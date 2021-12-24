using System;
using System.Collections.Generic;
using System.Linq;

namespace Confuser.Core {
	/// <summary>
	///     Protection settings for a certain component
	/// </summary>
	public class ProtectionSettings : Dictionary<IConfuserComponent, IDictionary<string, string>>, IProtectionSettings {
		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionSettings" /> class.
		/// </summary>
		public ProtectionSettings() {
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionSettings" /> class
		///     from an existing <see cref="ProtectionSettings" />.
		/// </summary>
		/// <param name="settings">The settings to copy from.</param>
		public ProtectionSettings(ProtectionSettings settings) {
			if (settings == null)
				return;

			foreach (var i in settings)
				Add(i.Key, new Dictionary<string, string>(i.Value));
		}

		public string GetParameter(IConfuserComponent component, string name) {
			if (component == null) throw new ArgumentNullException(nameof(component));
			if (name == null) throw new ArgumentNullException(nameof(name));

			if (TryGetValue(component, out var p)) {
				if (p.TryGetValue(name, out var result)) {
					return result;
				}

				throw new ArgumentException($"{name} is not set for {component.Name}", nameof(name));
			}

			throw new ArgumentException($"{component.Name} has no parameters", nameof(component));
		}

		/// <inheritdoc />
		public bool HasParameter(IConfuserComponent component, string name) {
			if (component == null) throw new ArgumentNullException(nameof(component));
			if (name == null) throw new ArgumentNullException(nameof(name));

			return TryGetValue(component, out var p) && p.ContainsKey(name);
		}

		public bool HasParameters(IConfuserComponent component) => component != null && ContainsKey(component);

		/// <summary>
		///     Determines whether the settings is empty.
		/// </summary>
		/// <returns><c>true</c> if the settings is empty; otherwise, <c>false</c>.</returns>
		public bool IsEmpty() => Count == 0;

		public void RemoveParameters(IConfuserComponent component) {
			if (component == null) return;
			Remove(component);
		}

		public void SetParameter(IConfuserComponent component, string name, string value) {
			if (component == null) throw new ArgumentNullException(nameof(component));
			if (name == null) throw new ArgumentNullException(nameof(name));

			if (!TryGetValue(component, out var p)) {
				p = new Dictionary<string, string>();
				Add(component, p);
			}

			p[name] = value;
		}
	}
}
