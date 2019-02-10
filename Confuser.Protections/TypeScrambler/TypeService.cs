using System.Collections.Generic;
using Confuser.Core;
using Confuser.Protections.Services;
using Confuser.Protections.TypeScramble.Scrambler;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble {
	internal sealed class TypeService : ITypeScrambleService {
		private Dictionary<MDToken, ScannedItem> GenericsMapper = new Dictionary<MDToken, ScannedItem>();

		public void AddScannedItem(ScannedMethod m) {
			ScannedItem typescan;
			if (GenericsMapper.TryGetValue(m.TargetMethod.DeclaringType.MDToken, out typescan)) {
				m.GenericCount += typescan.GenericCount;
			}

			AddScannedItemGeneral(m);
		}

		public void AddScannedItem(ScannedType m) {
			//AddScannedItemGeneral(m);
		}

		private void AddScannedItemGeneral(ScannedItem m) {
			m.Scan();
			if (!GenericsMapper.ContainsKey(m.GetToken())) {
				GenericsMapper.Add(m.GetToken(), m);
			}
		}

		public void PrepairItems() {
			foreach (var item in GenericsMapper.Values) {
				item.PrepairGenerics();
			}
		}

		public ScannedItem GetItem(MDToken token) {
			ScannedItem i = null;
			GenericsMapper.TryGetValue(token, out i);
			return i;
		}
	}
}
