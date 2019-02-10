using System;
using System.Linq;
using System.Threading;
using Confuser.Core;

namespace Confuser.Protections.AntiTamper {
	internal sealed class MetadataPhase : IProtectionPhase {
		public MetadataPhase(AntiTamperProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiTamperProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Anti-tamper metadata preparation";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			if (!parameters.Targets.Any())
				return;

			var modeHandler =
				context.Annotations.Get<IModeHandler>(context.CurrentModule, AntiTamperProtection.HandlerKey);
			modeHandler.HandleMD(Parent, context, parameters);
		}
	}
}
