using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
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

				// Inject helpers
				var decomp = compression.GetRuntimeDecompressor(context, context.CurrentModule, member => {
					name?.MarkHelper(context, member, marker, Parent);
					if (member is MethodDef)
						context.GetParameters(member).RemoveParameters(Parent);
				});
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

			var initInjectResult = Helpers.InjectHelper.Inject(constantRuntime.FindMethod("Initialize"), context.CurrentModule,
				Helpers.InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType, name),
				new Helpers.MutationProcessor(context.Registry, context.CurrentModule) {
					CryptProcessor = moduleCtx.ModeHandler.EmitDecrypt(moduleCtx)
				});
			moduleCtx.InitMethod = initInjectResult.Requested.Mapped;
			moduleCtx.Name?.MarkHelper(context, moduleCtx.InitMethod, moduleCtx.Marker, Parent);

			var dataType = new TypeDefUser("", moduleCtx.Name.RandomName(), context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
				Layout = TypeAttributes.ExplicitLayout,
				Visibility = TypeAttributes.NestedPrivate,
				IsSealed = true
			};
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			moduleCtx.Name?.MarkHelper(context, dataType, moduleCtx.Marker, Parent);

			moduleCtx.DataField = new FieldDefUser(moduleCtx.Name.RandomName(), new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			moduleCtx.Name?.MarkHelper(context, moduleCtx.DataField, moduleCtx.Marker, Parent);

			var decoderDesc = new DecoderDesc();
			decoderDesc.StringID = (byte)(moduleCtx.Random.NextByte() & 3);
			do
				decoderDesc.NumberID = (byte)(moduleCtx.Random.NextByte() & 3);
			while (decoderDesc.NumberID == decoderDesc.StringID);

			do
				decoderDesc.InitializerID = (byte)(moduleCtx.Random.NextByte() & 3);
			while (decoderDesc.InitializerID == decoderDesc.StringID || decoderDesc.InitializerID == decoderDesc.NumberID);

			var mutationKeys = ImmutableDictionary.Create<Helpers.MutationField, int>()
				.Add(Helpers.MutationField.KeyI0, decoderDesc.StringID)
				.Add(Helpers.MutationField.KeyI1, decoderDesc.NumberID)
				.Add(Helpers.MutationField.KeyI2, decoderDesc.InitializerID);

			var decoder = rt.GetRuntimeType("Confuser.Runtime.Constant").FindMethod("Get");

			moduleCtx.Decoders = new List<Tuple<MethodDef, DecoderDesc>>();
			for (int i = 0; i < moduleCtx.DecoderCount; i++) {
				using (Helpers.InjectHelper.CreateChildContext()) {
					var decoderImpl = moduleCtx.ModeHandler.CreateDecoder(moduleCtx);

					var decoderInjectResult = Helpers.InjectHelper.Inject(decoder, moduleCtx.Module,
						Helpers.InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType, name),
						new Helpers.MutationProcessor(context.Registry, context.CurrentModule) {
							KeyFieldValues = mutationKeys,
							PlaceholderProcessor = decoderImpl.Processor
						});

					var decoderInst = decoderInjectResult.Requested.Mapped;
					moduleCtx.Name?.MarkHelper(context, decoderInst, moduleCtx.Marker, Parent);
					context.GetParameters(decoderInst).RemoveParameters(Parent);
					decoderDesc.Data = decoderImpl.Data;
					moduleCtx.Decoders.Add(Tuple.Create(decoderInst, decoderDesc));
				}
			}
		}
	}
}
