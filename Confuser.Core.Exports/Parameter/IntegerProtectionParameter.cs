using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;

namespace Confuser.Core.Parameter {
	internal sealed class IntegerProtectionParameter : IProtectionParameter<int> {

		public int DefaultValue { get; }

		public string Name {get;}

		internal IntegerProtectionParameter(string name, int defaultValue) {
			Debug.Assert(!string.IsNullOrWhiteSpace(name));

			Name = name;
			DefaultValue = defaultValue;
		}

		int IProtectionParameter<int>.Deserialize(string serializedValue) {
			if (int.TryParse(serializedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
				return value;

			throw new SerializationException($"Value {serializedValue} can't be deserialized to integer.");
		}

		string IProtectionParameter<int>.Serialize(int value) => value.ToString(CultureInfo.InvariantCulture);
	}
}
