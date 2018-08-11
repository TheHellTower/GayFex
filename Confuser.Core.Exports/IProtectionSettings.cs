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
		string GetParameter(IConfuserComponent component, string name);

		bool HasParameters(IConfuserComponent component);

		void RemoveParameters(IConfuserComponent component);
	}
}
