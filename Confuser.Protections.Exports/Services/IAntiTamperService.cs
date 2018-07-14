using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Protections.Services {
	public interface IAntiTamperService {
		void ExcludeMethod(IConfuserContext context, MethodDef method);
	}
}
