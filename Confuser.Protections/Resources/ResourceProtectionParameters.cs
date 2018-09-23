using Confuser.Core;

namespace Confuser.Protections.Resources {
	internal sealed class ResourceProtectionParameters {
		internal IProtectionParameter<Mode> Mode { get; } = ProtectionParameter.Enum("mode", Resources.Mode.Normal);
	}
}
