using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading;

namespace Confuser.Core {
	public abstract class ProtectionParametersBase : IReadOnlyDictionary<string, IProtectionParameter> {
		private readonly Lazy<IReadOnlyDictionary<string, IProtectionParameter>> _lazyReadOnlyDictionaryImplementation;

		private IReadOnlyDictionary<string, IProtectionParameter> ReadOnlyDictionaryImplementation =>
			_lazyReadOnlyDictionaryImplementation.Value;

		protected ProtectionParametersBase() =>
			_lazyReadOnlyDictionaryImplementation =
				new Lazy<IReadOnlyDictionary<string, IProtectionParameter>>(CreateDictionary,
					LazyThreadSafetyMode.None);

		private IReadOnlyDictionary<string, IProtectionParameter> CreateDictionary() {
			var targetType = GetType();

			var resultBuilder =
				ImmutableDictionary.CreateBuilder<string, IProtectionParameter>(StringComparer.OrdinalIgnoreCase);

			var properties =
				targetType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			foreach (var prop in properties) {
				if (!typeof(IProtectionParameter).IsAssignableFrom(prop.PropertyType)) continue;
				var getMethod = prop.GetMethod;
				if (getMethod == null) continue;
				if (getMethod.Invoke(this, Array.Empty<object>()) is IProtectionParameter param) {
					resultBuilder.Add(param.Name, param);
				}
			}

			return resultBuilder.ToImmutable();
		}

		#region IReadOnlyDictionary

		IEnumerator<KeyValuePair<string, IProtectionParameter>> IEnumerable<KeyValuePair<string, IProtectionParameter>>.
			GetEnumerator() =>
			ReadOnlyDictionaryImplementation.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)ReadOnlyDictionaryImplementation).GetEnumerator();

		int IReadOnlyCollection<KeyValuePair<string, IProtectionParameter>>.Count =>
			ReadOnlyDictionaryImplementation.Count;

		bool IReadOnlyDictionary<string, IProtectionParameter>.ContainsKey(string key) =>
			ReadOnlyDictionaryImplementation.ContainsKey(key);

		bool IReadOnlyDictionary<string, IProtectionParameter>.
			TryGetValue(string key, out IProtectionParameter value) =>
			ReadOnlyDictionaryImplementation.TryGetValue(key, out value);

		IProtectionParameter IReadOnlyDictionary<string, IProtectionParameter>.this[string key] =>
			ReadOnlyDictionaryImplementation[key];

		IEnumerable<string> IReadOnlyDictionary<string, IProtectionParameter>.Keys =>
			ReadOnlyDictionaryImplementation.Keys;

		IEnumerable<IProtectionParameter> IReadOnlyDictionary<string, IProtectionParameter>.Values =>
			ReadOnlyDictionaryImplementation.Values;

		#endregion
	}
}
