using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;

namespace Confuser.Core.Parameter {
	internal sealed class PercentProtectionParameter : IProtectionParameter<double> {

		public double DefaultValue { get; }

		public string Name {get;}

		internal PercentProtectionParameter(string name, double defaultValue) {
			Debug.Assert(!string.IsNullOrWhiteSpace(name) && defaultValue >= 0.0 && defaultValue <= 1.0);

			Name = name;
			DefaultValue = defaultValue;
		}

		double IProtectionParameter<double>.Deserialize(string serializedValue) {
			if (double.TryParse(serializedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
				return Math.Min(1.0, Math.Max(0.0, value / 100.0));

			throw new SerializationException($"Value {serializedValue} can't be deserialized to percentage.");
		}

		string IProtectionParameter<double>.Serialize(double value) {
			Debug.Assert(value >= 0.0 && value <= 1.0);
			return (value * 100.0).ToString(CultureInfo.InvariantCulture);
		}
	}
}
