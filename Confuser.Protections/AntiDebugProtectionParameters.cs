using Confuser.Core;

namespace Confuser.Protections {
	internal sealed class AntiDebugProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<AntiDebugMode> Mode { get; } = ProtectionParameter.Enum("mode", AntiDebugMode.Safe);
	}
}
