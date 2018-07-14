namespace Confuser.Core {
	/// <summary>
	///     Base class of Confuser protections.
	/// </summary>
	/// <remarks>
	///     A parameterless constructor must exists in derived classes to enable plugin discovery.
	/// </remarks>
	public interface IProtection : IConfuserComponent {
		/// <summary>
		///     Gets the preset this protection is in.
		/// </summary>
		/// <value>The protection's preset.</value>
		ProtectionPreset Preset { get; }
	}
}
