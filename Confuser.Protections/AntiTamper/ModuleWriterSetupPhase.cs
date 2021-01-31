using System;
using System.Linq;
using System.Threading;
using Confuser.Core;
using dnlib.DotNet.Writer;

namespace Confuser.Protections.AntiTamper {
	internal sealed class ModuleWriterSetupPhase : IProtectionPhase {
		public ModuleWriterSetupPhase(AntiTamperProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiTamperProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		/// <inheritdoc />
		public string Name => "Anti-tamper module writer preparation";

		public bool ProcessAll => false;

		/// <inheritdoc />
		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (!parameters.Targets.Any()) return;

			if (context.CurrentModuleWriterOptions is NativeModuleWriterOptions nativeOptions) {
				context.RequestNative(false);
			}
		}
	}
}
