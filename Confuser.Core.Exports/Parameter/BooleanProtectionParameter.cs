using System.Diagnostics;
using System.Runtime.Serialization;

namespace Confuser.Core.Parameter {
	internal sealed class BooleanProtectionParameter : IProtectionParameter<bool> {

		public bool DefaultValue { get; }

		public string Name { get; }

		internal BooleanProtectionParameter(string name, bool defaultValue) {
			Debug.Assert(!string.IsNullOrWhiteSpace(name));

			Name = name;
			DefaultValue = defaultValue;
		}

		bool IProtectionParameter<bool>.Deserialize(string serializedValue) {
			switch (serializedValue.ToUpperInvariant()) {
				case "TRUE":
				case "T":
				case "YES":
				case "Y":
				case "1":
					return true;
				case "FALSE":
				case "F":
				case "NO":
				case "N":
				case "0":
					return false;
			}
			throw new SerializationException($"Value {serializedValue} can't be deserialized to boolean.");
		}

		string IProtectionParameter<bool>.Serialize(bool value) => value ? "true" : "false";
	}
}
