using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Confuser.Core.Parameter {
	internal sealed class EnumProtectionParameter<T> : IProtectionParameter<T> where T : struct {
		public T DefaultValue { get; }

		public string Name { get; }

		internal EnumProtectionParameter(string name, T defaultValue) {
			Debug.Assert(!string.IsNullOrWhiteSpace(name) && typeof(T).IsEnum && Enum.IsDefined(typeof(T), defaultValue));

			Name = name;
			DefaultValue = defaultValue;
		}

		T IProtectionParameter<T>.Deserialize(string serializedValue) {
			if (Enum.TryParse<T>(serializedValue, true, out var result))
				return result;

			throw new SerializationException($"Value {serializedValue} can't be deserialized to enum {typeof(T).FullName}");
		}

		string IProtectionParameter<T>.Serialize(T value) => Enum.GetName(typeof(T), value);
	}
}
