namespace Confuser.Core {
	public interface IProtectionSettings {
		void SetParameter(IConfuserComponent component, string name, string value);

		/// <summary>
		///     Get the parameter for the component store under the specified name.
		/// </summary>
		/// <param name="component">The component</param>
		/// <param name="name">The name used as key</param>
		/// <returns>The value of the parameter or <see langword="null" />.</returns>
		/// <exception cref="System.ArgumentNullException">
		///   <paramref name="component"/> is <see langword="null" />
		///   <br />- or -<br />
		///   <paramref name="name"/> is <see langword="null" />
		/// </exception>
		/// <exception cref="System.ArgumentException">
		///   No parameter with the <paramref name="name"/> is set.
		/// </exception>
		string GetParameter(IConfuserComponent component, string name);

		/// <summary>
		/// Check if the parameter is defined.
		/// </summary>
		/// <param name="component">The component</param>
		/// <param name="name">The name used as key</param>
		/// <returns><see langword="true"/> if the parameter is set</returns>
		bool HasParameter(IConfuserComponent component, string name);

		bool HasParameters(IConfuserComponent component);

		void RemoveParameters(IConfuserComponent component);
	}
}
