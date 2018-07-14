using dnlib.DotNet;

namespace Confuser.Core.Services {
	/// <summary>
	///     Provides methods to obtain runtime library injection type.
	/// </summary>
	public interface IRuntimeService {
		/// <summary>
		///     Gets the specified runtime type for injection.
		/// </summary>
		/// <param name="fullName">The full name of the runtime type.</param>
		/// <returns>The requested runtime type.</returns>
		TypeDef GetRuntimeType(string fullName);
	}
}
