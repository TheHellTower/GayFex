using System.Collections.Generic;

namespace Confuser.Core {
	/// <summary>
	///     Base class of Confuser protections.
	/// </summary>
	/// <inheritdoc />
	public interface IProtection : IConfuserComponent {
		/// <summary>
		///     Gets the preset this protection is in.
		/// </summary>
		/// <value>The protection's preset.</value>
		ProtectionPreset Preset { get; }

		/// <summary>
		///     Get the parameters of this protection.
		/// </summary>
		IReadOnlyDictionary<string, IProtectionParameter> Parameters { get; }
	}
}
