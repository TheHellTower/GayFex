using System.Diagnostics;

namespace Confuser.Core.Parameter {
	internal sealed class StringProtectionParameter : IProtectionParameter<string> {
		public string DefaultValue { get; }

		public string Name { get; }

		internal StringProtectionParameter(string name, string defaultValue) {
			Debug.Assert(!string.IsNullOrWhiteSpace(name));

			Name = name;
			DefaultValue = defaultValue;
		}

		string IProtectionParameter<string>.Deserialize(string serializedValue) => serializedValue;

		string IProtectionParameter<string>.Serialize(string value) => value;
	}
}
