using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Constants {
	internal sealed class InjectPhase : IProtectionPhase {
		public InjectPhase(ConstantProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ConstantProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Constant encryption helpers injection";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (parameters.Targets.Any()) {
				var compression = context.Registry.GetRequiredService<ICompressionService>();
				var name = context.Registry.GetService<INameService>();
				var marker = context.Registry.GetRequiredService<IMarkerService>();
				var moduleCtx = new CEContext {
					Protection = Parent,
					Random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(ConstantProtection._FullId),
					Context = context,
					Module = context.CurrentModule,
					Marker = marker,
					DynCipher = context.Registry.GetRequiredService<IDynCipherService>(),
					Name = name,
					Trace = context.Registry.GetRequiredService<ITraceService>()
				};

				// Extract parameters
				moduleCtx.Mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Normal);
				moduleCtx.DecoderCount = parameters.GetParameter(context, context.CurrentModule, "decoderCount", 5);

				switch (moduleCtx.Mode) {
					case Mode.Normal:
						moduleCtx.ModeHandler = new NormalMode();
						break;
					case Mode.Dynamic:
						moduleCtx.ModeHandler = new DynamicMode();
						break;
					case Mode.x86:
						moduleCtx.ModeHandler = new x86Mode();
						if ((context.CurrentModule.Cor20HeaderFlags & ComImageFlags.ILOnly) != 0)
							context.CurrentModuleWriterOptions.Cor20HeaderOptions.Flags &= ~ComImageFlags.ILOnly;
						break;
					default:
						throw new UnreachableException();
				}

				InjectHelpers(context, moduleCtx);
				var cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));

				context.Annotations.Set(context.CurrentModule, ConstantProtection.ContextKey, moduleCtx);
			}
		}

		void InjectHelpers(IConfuserContext context, CEContext moduleCtx) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(moduleCtx != null, $"{nameof(moduleCtx)} != null");

			var rt = context.Registry.GetRequiredService<IRuntimeService>();
			var name = context.Registry.GetRequiredService<INameService>();
			var constantRuntime = rt.GetRuntimeType("Confuser.Runtime.Constant");
			Debug.Assert(constantRuntime != null, $"{nameof(constantRuntime)} != null");

			var lateMutationFields = ImmutableDictionary.Create<MutationField, LateMutationFieldUpdate>()
				.Add(MutationField.KeyI0, moduleCtx.EncodingBufferSizeUpdate)
				.Add(MutationField.KeyI1, moduleCtx.KeySeedUpdate);

			var initInjectResult = InjectHelper.Inject(constantRuntime.FindMethod("Initialize"), context.CurrentModule,
				InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType),
				new MutationProcessor(context.Registry, context.CurrentModule) {
					CryptProcessor = moduleCtx.ModeHandler.EmitDecrypt(moduleCtx),
					PlaceholderProcessor = CreateDataField(context, moduleCtx),
					LateKeyFieldValues = lateMutationFields
				});
			moduleCtx.InitMethod = initInjectResult.Requested.Mapped;
			name?.MarkHelper(context, moduleCtx.InitMethod, moduleCtx.Marker, Parent);

			var decoder = rt.GetRuntimeType("Confuser.Runtime.Constant").FindMethod("Get");

			moduleCtx.Decoders = new List<Tuple<MethodDef, DecoderDesc>>();
			for (int i = 0; i < moduleCtx.DecoderCount; i++) {
				Span<byte> ids = stackalloc byte[3] { 0, 1, 2 };
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

				using (InjectHelper.CreateChildContext()) {
					var decoderImpl = moduleCtx.ModeHandler.CreateDecoder(moduleCtx);

					var decoderInjectResult = InjectHelper.Inject(decoder, moduleCtx.Module,
						InjectBehaviors.RenameAndInternalizeBehavior(context),
						new MutationProcessor(context.Registry, context.CurrentModule) {
							KeyFieldValues = mutationKeys,
							PlaceholderProcessor = decoderImpl.Processor
						});

					var decoderInst = decoderInjectResult.Requested.Mapped;
					name?.MarkHelper(context, decoderInst, moduleCtx.Marker, Parent);
					context.GetParameters(decoderInst).RemoveParameters(Parent);
					decoderDesc.Data = decoderImpl.Data;
					moduleCtx.Decoders.Add(Tuple.Create(decoderInst, decoderDesc));
				}
			}
		}

		private PlaceholderProcessor CreateDataField(IConfuserContext context, CEContext moduleCtx) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(moduleCtx != null, $"{nameof(moduleCtx)} != null");

			var name = context.Registry.GetRequiredService<INameService>();

			var dataType = new TypeDefUser("", name.RandomName(), context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
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
				repl.Add(Instruction.Create(OpCodes.Call, context.CurrentModule.Import(
					typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.InitializeArray)))));
				return repl;
			};
		}
	}
}
