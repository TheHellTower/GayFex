using System;
using System.Linq;
using System.Threading;
using Confuser.Core;

namespace Confuser.Protections.AntiTamper {
	internal sealed class InjectPhase : IProtectionPhase {
		public InjectPhase(AntiTamperProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiTamperProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Anti-tamper helpers injection";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (!parameters.Targets.Any())
				return;

			AntiTamperMode mode = parameters.GetParameter(context, context.CurrentModule, Parent.Parameters.Mode);
			IModeHandler modeHandler;
			switch (mode) {
			case AntiTamperMode.Normal:
				modeHandler = new NormalMode();
				break;
			case AntiTamperMode.JIT:
				modeHandler = new JITMode();
				break;
			default:
				throw new UnreachableException();
			}
			modeHandler.HandleInject(Parent, context, parameters);
			context.Annotations.Set(context.CurrentModule, AntiTamperProtection.HandlerKey, modeHandler);
		}
	}
}
