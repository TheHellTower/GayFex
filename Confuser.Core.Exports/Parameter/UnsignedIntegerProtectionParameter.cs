using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;

namespace Confuser.Core.Parameter {
	internal sealed class UnsignedIntegerProtectionParameter : IProtectionParameter<uint> {

		public uint DefaultValue { get; }

		public string Name {get;}

		internal UnsignedIntegerProtectionParameter(string name, uint defaultValue) {
			Debug.Assert(!string.IsNullOrWhiteSpace(name));

			Name = name;
			DefaultValue = defaultValue;
		}

		uint IProtectionParameter<uint>.Deserialize(string serializedValue) {
			if (uint.TryParse(serializedValue, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out uint value))
				return value;

			throw new SerializationException($"Value {serializedValue} can't be deserialized to unsigned integer.");
		}

		string IProtectionParameter<uint>.Serialize(uint value) => value.ToString(CultureInfo.InvariantCulture);
	}
}
