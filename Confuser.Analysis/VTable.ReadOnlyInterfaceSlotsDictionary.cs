using System.Collections;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Analysis {

	internal partial class VTable {
		private sealed class ReadOnlyInterfaceSlotsDictionary : IReadOnlyDictionary<TypeSig, IReadOnlyList<IVTableSlot>> {
			private readonly Dictionary<TypeSig, List<VTableSlot>> _realDict;

			public ReadOnlyInterfaceSlotsDictionary(Dictionary<TypeSig, List<VTableSlot>> realDict) => _realDict = realDict;

			public IReadOnlyList<IVTableSlot> this[TypeSig key] => _realDict[key];

			public IEnumerable<TypeSig> Keys => _realDict.Keys;

			public IEnumerable<IReadOnlyList<IVTableSlot>> Values => _realDict.Values;

			public int Count => _realDict.Count;

			public bool ContainsKey(TypeSig key) => _realDict.ContainsKey(key);
			public IEnumerator<KeyValuePair<TypeSig, IReadOnlyList<IVTableSlot>>> GetEnumerator() {
				foreach (var kvp in _realDict) {
					yield return new KeyValuePair<TypeSig, IReadOnlyList<IVTableSlot>>(kvp.Key, kvp.Value);
				}
			}

			public bool TryGetValue(TypeSig key, out IReadOnlyList<IVTableSlot> value) {
				if (_realDict.TryGetValue(key, out var realValue)) {
					value = realValue;
					return true;
				} else {
					value = null;
					return false;
				}
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
