using Confuser.Core;

namespace Confuser.Protections.Compress {
	internal sealed class CompressorParameters {
		internal IProtectionParameter<bool> CompatMode { get; } = ProtectionParameter.Boolean("compat", false);

		internal IProtectionParameter<KeyDeriverMode> Key { get; } =
			ProtectionParameter.Enum("key", KeyDeriverMode.Normal);
	}
}
