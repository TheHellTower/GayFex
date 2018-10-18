using dnlib.DotNet;

namespace Confuser.Optimizations {
	public interface IRegexTargetMethods {
		IRegexTargetMethod GetMatchingMethod(IMethod method);
	}
}
