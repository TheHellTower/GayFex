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
	internal sealed class AntiDumpProtectionPhase : IProtectionPhase {
		public AntiDumpProtectionPhase(AntiDumpProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiDumpProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-dump injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var runtime = context.Registry.GetRequiredService<IRuntimeService>();
			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var name = context.Registry.GetRequiredService<INameService>();

			var rtType = runtime.GetRuntimeType("Confuser.Runtime.AntiDump");
			var initMethod = rtType.FindMethod("Initialize");

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var injectResult = InjectHelper.Inject(initMethod, module, InjectBehaviors.RenameAndNestBehavior(context, module.GlobalType, name));

				var cctor = module.GlobalType.FindStaticConstructor();
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, injectResult.Requested.Mapped));

				foreach (var dependencies in injectResult.InjectedDependencies)
					marker.Mark(context, dependencies.Mapped, Parent);
			}
		}
	}
}
