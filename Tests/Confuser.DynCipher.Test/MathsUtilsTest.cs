using System;
using Xunit;

namespace Confuser.DynCipher {
	public sealed class MathsUtilsTest {
		[Fact]
		[Trait("Category", "DynCipher")]
		[Trait("DynCipher", "Utilities")]
		public void TestModInv() {
			var rnd = new Random();

			int getKey(int index) {
				switch (index) {
					case 0: return 0 | 1;
					case 1: return int.MinValue | 1;
					case 2: return int.MaxValue | 1;
					default: return rnd.Next(int.MinValue, int.MaxValue) | 1;
				}
			}

			for (int i = 0; i < 10000; i++) {
				var key = getKey(i);
				var invKey = unchecked((int)MathsUtils.ModInv((uint)key));
				for (int k = 0; k < 10000; k++) {
					var payload = rnd.Next(int.MinValue, int.MaxValue);
					var modPayload = payload * key;
					var invPayload = modPayload * invKey;
					Assert.Equal(payload, invPayload);
				}
			}
		}
	}
}
