using dnlib.DotNet;

namespace Confuser.Core {
	public interface IRuntimeModule {
		TypeDef GetRuntimeType(string fullName, ModuleDef targetModule);
	}
}
