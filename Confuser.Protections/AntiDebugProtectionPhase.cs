using System;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections {
	class AntiDebugProtectionPhase : IProtectionPhase {
		public AntiDebugProtectionPhase(AntiDebugProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiDebugProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-debug injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var rt = context.Registry.GetRequiredService<IRuntimeService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var name = context.Registry.GetRequiredService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger(nameof(AntiDebugProtectionPhase));

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var mode = parameters.GetParameter(context, module, "mode", AntiDebugMode.Safe);

				TypeDef rtType = null;
				switch (mode) {
					case AntiDebugMode.Safe:
						rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugSafe");
						break;
					case AntiDebugMode.Win32:
						rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugWin32");
						break;
					case AntiDebugMode.Antinet:
						rtType = rt.GetRuntimeType("Confuser.Runtime.AntiDebugAntinet");
						break;
					default:
						throw new UnreachableException();
				}

				var initMethod = rtType.FindMethod("Initialize");
				if (initMethod == null) {
					logger.Error("Could not find \"Initialize\" for " + rtType.FullName);
					continue;
				}

				var injectResult = InjectHelper.Inject(initMethod, module, InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType, name));
				var cctor = module.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, injectResult.Requested.Mapped));

				foreach (var dependencies in injectResult.InjectedDependencies)
					marker.Mark(context, dependencies.Mapped, Parent);
			}
		}
	}
}
