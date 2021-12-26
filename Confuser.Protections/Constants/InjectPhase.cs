using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.Constants {
	internal sealed class InjectPhase : IProtectionPhase {
		public InjectPhase(ConstantProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ConstantProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Constant encryption helpers injection";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			if (!parameters.Targets.Any()) return;

			var moduleCtx = context.Annotations.Get<CEContext>(context.CurrentModule, ConstantProtection.ContextKey);
			if (moduleCtx?.EncodedReferences is null) return;

			if (moduleCtx.Mode == Mode.x86)
				if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
					context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;

			InjectHelpers(context, moduleCtx);
			var cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
			cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));
		}

		private void InjectHelpers(IConfuserContext context, CEContext moduleCtx) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(moduleCtx != null, $"{nameof(moduleCtx)} != null");

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(ConstantProtection._Id);
			var name = context.Registry.GetRequiredService<INameService>();
			var constantRuntime = GetRuntimeType(moduleCtx.Module, context, logger);
			Debug.Assert(constantRuntime != null, $"{nameof(constantRuntime)} != null");

			var injectHelper = context.Registry.GetRequiredService<ProtectionsRuntimeService>().InjectHelper;

			var lateMutationFields = ImmutableDictionary.Create<MutationField, LateMutationFieldUpdate>()
				.Add(MutationField.KeyI0, moduleCtx.EncodingBufferSizeUpdate)
				.Add(MutationField.KeyI1, moduleCtx.KeySeedUpdate);

			var initInjectResult = injectHelper.Inject(constantRuntime.FindMethod("Initialize"), context.CurrentModule,
				InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType),
				new CompressionServiceProcessor(context, context.CurrentModule, moduleCtx.UsedCompressionAlgorithm),
				new MutationProcessor(context.Registry, context.CurrentModule) {
					CryptProcessor = moduleCtx.ModeHandler.EmitDecrypt(moduleCtx),
					PlaceholderProcessor = CreateDataField(context, moduleCtx),
					LateKeyFieldValues = lateMutationFields
				});
			moduleCtx.InitMethod = initInjectResult.Requested.Mapped;
			name?.MarkHelper(context, moduleCtx.InitMethod, moduleCtx.Marker, Parent);

			var decoder = constantRuntime.FindMethod("Get");

			moduleCtx.Decoders = new List<(MethodDef, DecoderDesc)>();
			Span<byte> ids = stackalloc byte[3] { 0, 1, 2 };
			for (int i = 0; i < moduleCtx.DecoderCount; i++) {
				moduleCtx.Random.Shuffle(ids);

				var decoderDesc = new DecoderDesc {
					StringID = ids[0],
					NumberID = ids[1],
					InitializerID = ids[2]
				};

				var mutationKeys = ImmutableDictionary.Create<MutationField, int>()
					.Add(MutationField.KeyI0, decoderDesc.StringID)
					.Add(MutationField.KeyI1, decoderDesc.NumberID)
					.Add(MutationField.KeyI2, decoderDesc.InitializerID);

				using (injectHelper.CreateChildContext()) {
					var decoderImpl = moduleCtx.ModeHandler.CreateDecoder(moduleCtx);

					var decoderInjectResult = injectHelper.Inject(decoder, moduleCtx.Module,
						InjectBehaviors.RenameAndInternalizeBehavior(context),
						new MutationProcessor(context.Registry, context.CurrentModule) {
							KeyFieldValues = mutationKeys,
							PlaceholderProcessor = decoderImpl.Processor,
							ValueProcessor = (v1, v2, v3) => new []{Instruction.Create(OpCodes.Sizeof, new GenericMVar(0).ToTypeDefOrRef())}
						});

					var decoderInst = decoderInjectResult.Requested.Mapped;
					name?.MarkHelper(context, decoderInst, moduleCtx.Marker, Parent);
					context.GetParameters(decoderInst).RemoveParameters(Parent);
					decoderDesc.Data = decoderImpl.Data;
					moduleCtx.Decoders.Add((decoderInst, decoderDesc));
				}
			}
		}

		private static TypeDef GetRuntimeType(ModuleDef module, IConfuserContext context, ILogger logger) {
			Debug.Assert(module != null, $"{nameof(module)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(logger != null, $"{nameof(logger)} != null");

			var rt = context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();

			const string runtimeTypeName = "Confuser.Runtime.Constant";

			TypeDef rtType = null;
			try {
				rtType = rt.GetRuntimeType(runtimeTypeName, module);
			}
			catch (ArgumentException ex) {
				logger.LogError("Failed to load runtime: {0}", ex.Message);
				return null;
			}

			if (rtType == null) {
				logger.LogError("Failed to load runtime: {0}", runtimeTypeName);
				return null;
			}

			return rtType;
		}

		private PlaceholderProcessor CreateDataField(IConfuserContext context, CEContext moduleCtx) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(moduleCtx != null, $"{nameof(moduleCtx)} != null");

			var name = context.Registry.GetRequiredService<INameService>();

			var dataType = new TypeDefUser("", name.RandomName(),
				context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
				Layout = TypeAttributes.ExplicitLayout,
				Visibility = TypeAttributes.NestedPrivate,
				IsSealed = true
			};
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			name?.MarkHelper(context, dataType, moduleCtx.Marker, Parent);

			moduleCtx.DataField = new FieldDefUser(name.RandomName(), new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			name?.MarkHelper(context, moduleCtx.DataField, moduleCtx.Marker, Parent);

			return (module, method, args) => {
				var repl = new List<Instruction>(args.Count + 3);
				repl.AddRange(args);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));

				var runtimeHelper =
					context.CurrentModule.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", "RuntimeHelpers");
				var initArrayDef = runtimeHelper.ResolveThrow().FindMethod("InitializeArray");
				repl.Add(Instruction.Create(OpCodes.Call, context.CurrentModule.Import(initArrayDef)));
				return repl;
			};
		}
	}
}
