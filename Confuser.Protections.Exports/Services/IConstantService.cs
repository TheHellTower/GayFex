using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections.Services {
	public interface IConstantService {
		void ExcludeMethod(IConfuserContext context, MethodDef method);
	}
}
