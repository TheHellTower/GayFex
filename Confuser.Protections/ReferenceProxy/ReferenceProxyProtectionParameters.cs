using Confuser.Core;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed class ReferenceProxyProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<Mode> Mode { get; } = ProtectionParameter.Enum("mode", ReferenceProxy.Mode.Mild);

		internal IProtectionParameter<EncodingType> Encoding { get; } =
			ProtectionParameter.Enum("encoding", EncodingType.Normal);

		internal IProtectionParameter<bool> InternalAlso { get; } = ProtectionParameter.Boolean("internal", false);
		internal IProtectionParameter<bool> EraseTypes { get; } = ProtectionParameter.Boolean("typeErasure", false);
		internal IProtectionParameter<uint> Depth { get; } = ProtectionParameter.UInteger("depth", 3);
		internal IProtectionParameter<uint> InitCount { get; } = ProtectionParameter.UInteger("initCount", 16);
	}
}
