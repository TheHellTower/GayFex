using System;
using System.Linq;
using System.Threading;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections {
	class AntiILDasmProtectionPhase : IProtectionPhase {
		public AntiILDasmProtectionPhase(AntiILDasmProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public AntiILDasmProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Modules;

		public string Name => "Anti-ILDasm marking";

		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var attrRef =
					module.CorLibTypes.GetTypeRef("System.Runtime.CompilerServices", "SuppressIldasmAttribute");
				var ctorRef = new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void),
					attrRef);

				var attr = new CustomAttribute(ctorRef);
				module.CustomAttributes.Add(attr);

				token.ThrowIfCancellationRequested();
			}
		}
	}
}
