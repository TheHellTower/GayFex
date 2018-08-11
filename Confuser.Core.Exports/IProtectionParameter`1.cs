namespace Confuser.Core {
	public interface IProtectionParameter<T> : IProtectionParameter {
		T DefaultValue { get; }

		T Deserialize(string serializedValue);
		string Serialize(T value);
	}
}
