using System;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
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
			var rtType = context.Registry.GetRequiredService<IRuntimeService>().GetRuntimeType("Confuser.Runtime.AntiDump");

			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var name = context.Registry.GetService<INameService>();

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var members = InjectHelper.Inject(rtType, module.GlobalType, module);

				var cctor = module.GlobalType.FindStaticConstructor();
				var init = (MethodDef)members.Single(method => method.Name == "Initialize");
				cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, init));

				if (name != null) {
					foreach (var member in members)
						name.MarkHelper(context, member, marker, Parent);
				}
			}
		}
	}
}
