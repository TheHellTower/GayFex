using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections.Services {
	public interface IReferenceProxyService {
		void ExcludeMethod(IConfuserContext context, MethodDef method);
		void ExcludeTarget(IConfuserContext context, MethodDef method);
		bool IsTargeted(IConfuserContext context, MethodDef method);
	}
}
