using Confuser.Core;

namespace Confuser.Protections.ControlFlow {
	internal sealed class ControlFlowProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<CFType> ControlFlowType { get; } = ProtectionParameter.Enum("type", CFType.Switch);
		internal IProtectionParameter<PredicateType> PredicateType { get; } = ProtectionParameter.Enum("type", ControlFlow.PredicateType.Normal);
		internal IProtectionParameter<double> Intensity { get; } = ProtectionParameter.Percent("intensity", 0.6);
		internal IProtectionParameter<uint> Depth { get; } = ProtectionParameter.UInteger("depth", 4);
		internal IProtectionParameter<bool> AddJunkCode { get; } = ProtectionParameter.Boolean("junk", false);
	}
}
