using dnlib.DotNet;

namespace Confuser.Core.Services {
	/// <summary>
	///     Provides methods to obtain runtime library injection type.
	/// </summary>
	public interface IRuntimeService {
		IRuntimeModuleBuilder CreateRuntimeModule(string name);

		IRuntimeModule GetRuntimeModule(string name);
	}
}
