using Confuser.Core;

namespace Confuser.Protections {
	internal sealed class AntiDebugProtectionParameters {
		internal IProtectionParameter<AntiDebugMode> Mode { get; } = ProtectionParameter.Enum("mode", AntiDebugMode.Safe);
	}
}
