using Confuser.Core;

namespace Confuser.Protections.AntiTamper {
	internal sealed class AntiTamperProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<AntiTamperMode> Mode { get; } =
			ProtectionParameter.Enum("mode", AntiTamperMode.Normal);

		internal IProtectionParameter<KeyDeriverMode> Key { get; } =
			ProtectionParameter.Enum("key", KeyDeriverMode.Normal);
	}
}
