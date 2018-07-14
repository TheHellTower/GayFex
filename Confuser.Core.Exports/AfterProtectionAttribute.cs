using System;

namespace Confuser.Core {
	/// <summary>
	///     Indicates the <see cref="IProtection" /> must initialize after the specified protections.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class AfterProtectionAttribute : Attribute {
		/// <summary>
		///     Initializes a new instance of the <see cref="BeforeProtectionAttribute" /> class.
		/// </summary>
		/// <param name="ids">The full IDs of the specified protections.</param>
		public AfterProtectionAttribute(params string[] ids) {
			Ids = ids;
		}

		/// <summary>
		///     Gets the full IDs of the specified protections.
		/// </summary>
		/// <value>The IDs of protections.</value>
		public string[] Ids { get; private set; }
	}
}
