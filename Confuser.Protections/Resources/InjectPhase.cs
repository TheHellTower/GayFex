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
using Confuser.Protections.Services;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Resources {
	internal sealed class InjectPhase : IProtectionPhase {
		public InjectPhase(ResourceProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public ResourceProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Resource encryption helpers injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (parameters.Targets.Any()) {
				var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger(ResourceProtection._Id);
				if (!UTF8String.IsNullOrEmpty(context.CurrentModule.Assembly.Culture)) {
					logger.DebugFormat("Skipping resource encryption for satellite assembly '{0}'.",
									   context.CurrentModule.Assembly.FullName);
					return;
				}
				var name = context.Registry.GetService<INameService>();
				var marker = context.Registry.GetRequiredService<IMarkerService>();
				var moduleCtx = new REContext {
					Random = context.Registry.GetRequiredService<IRandomService>().GetRandomGenerator(ResourceProtection._FullId),
					Context = context,
					Module = context.CurrentModule,
					Marker = marker,
					DynCipher = context.Registry.GetRequiredService<IDynCipherService>(),
					Name = name,
					Trace = context.Registry.GetRequiredService<ITraceService>()
				};

				// Extract parameters
				moduleCtx.Mode = parameters.GetParameter(context, context.CurrentModule, "mode", Mode.Normal);

				switch (moduleCtx.Mode) {
				case Mode.Normal:
					moduleCtx.ModeHandler = new NormalMode();
					break;
				case Mode.Dynamic:
					moduleCtx.ModeHandler = new DynamicMode();
					break;
				default:
					throw new UnreachableException();
				}

				InjectHelpers(context, moduleCtx);

				var cctor = context.CurrentModule.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, moduleCtx.InitMethod));

				new MDPhase(moduleCtx).Hook(token);
			}
		}

		private void InjectHelpers(IConfuserContext context, REContext moduleCtx) {
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(moduleCtx != null, $"{nameof(moduleCtx)} != null");

			var rt = context.Registry.GetRequiredService<IRuntimeService>();
			var name = context.Registry.GetRequiredService<INameService>();
			var constant = context.Registry.GetRequiredService<IConstantService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();

			var dataType = new TypeDefUser("", "ConfuserResourceData", context.CurrentModule.CorLibTypes.GetTypeRef("System", "ValueType")) {
				Layout = TypeAttributes.ExplicitLayout,
				Visibility = TypeAttributes.NestedPrivate,
				IsSealed = true,
				ClassLayout = new ClassLayoutUser(1, 0)
			};
			moduleCtx.DataType = dataType;
			context.CurrentModule.GlobalType.NestedTypes.Add(dataType);
			name?.MarkHelper(context, dataType, marker, Parent);

			moduleCtx.DataField = new FieldDefUser("_ConfuserResourceData", new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = new byte[0],
				Access = FieldAttributes.CompilerControlled
			};
			context.CurrentModule.GlobalType.Fields.Add(moduleCtx.DataField);
			name?.MarkHelper(context, moduleCtx.DataField, marker, Parent);

			var rtName = context.Packer != null ? "Confuser.Runtime.Resource_Packer" : "Confuser.Runtime.Resource";
			var rtType = rt.GetRuntimeType(rtName);
			var rtInitMethod = rtType.FindMethod("Initialize");

			var lateMutationKeys = ImmutableDictionary.Create<MutationField, LateMutationFieldUpdate>()
				.Add(MutationField.KeyI0, moduleCtx.loadSizeUpdate)
				.Add(MutationField.KeyI1, moduleCtx.loadSeedUpdate);

			var injectResult = InjectHelper.Inject(rtInitMethod, context.CurrentModule,
				InjectBehaviors.RenameAndNestBehavior(context, context.CurrentModule.GlobalType),
				new MutationProcessor(context.Registry, context.CurrentModule) {
					CryptProcessor = moduleCtx.ModeHandler.EmitDecrypt(moduleCtx),
					PlaceholderProcessor = (module, method, arg) => {
						var repl = new List<Instruction>(arg.Count + 3);
						repl.AddRange(arg);
						repl.Add(Instruction.Create(OpCodes.Dup));
						repl.Add(Instruction.Create(OpCodes.Ldtoken, moduleCtx.DataField));
						repl.Add(Instruction.Create(OpCodes.Call, moduleCtx.Module.Import(
							typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.InitializeArray)))));
						return repl;
					},
					LateKeyFieldValues = lateMutationKeys
				});

			moduleCtx.InitMethod = injectResult.Requested.Mapped;
			foreach (var member in injectResult) {
				name?.MarkHelper(context, member.Mapped, marker, Parent);
			}
			constant.ExcludeMethod(context, injectResult.Requested.Mapped);
		}
	}
}
