using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections.Services {
	public interface IControlFlowService {
		void ExcludeMethod(IConfuserContext context, MethodDef method);
	}
}
