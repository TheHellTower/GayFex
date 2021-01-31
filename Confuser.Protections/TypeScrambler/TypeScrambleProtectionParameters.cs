using Confuser.Core;

namespace Confuser.Protections.TypeScrambler {
	internal sealed class TypeScrambleProtectionParameters : ProtectionParametersBase {
		internal IProtectionParameter<bool> ScramblePublic { get; } =
			ProtectionParameter.Boolean("scramblePublic", false);
	}
}
