namespace Confuser.DynCipher {
	public static class MathsUtils {
		private const ulong MODULO32 = 0x100000000;
		private const ulong MODULO8 = 0x100;

		/// <summary>
		/// Calculate the modular inverse of <paramref name="n"/> modulus <paramref name="m"/>.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="m"></param>
		/// <returns>
		/// a natural number smaller <paramref name="m"/>,
		/// if <paramref name="n"/> and <paramref name="m"/> are coprime
		/// </returns>
		/// <remarks>
		/// The modular inverse of <paramref name="n"/> modulo <paramref name="m"/> is
		/// the unique natural number <c>0 &lt; n0 &lt; m</c> such that <c>n * n0 = 1 mod m</c>.
		/// </remarks>
		public static ulong ModInv(ulong n, ulong m) {
			ulong a = m, b = n % m;
			ulong p0 = 0, p1 = 1;
			while (b != 0) {
				if (b == 1) return p1;
				p0 += (a / b) * p1;
				a %= b;

				if (a == 0) break;
				if (a == 1) return m - p0;

				p1 += (b / a) * p0;
				b %= a;
			}

			return 0;
		}

		public static uint ModInv(uint n) => (uint)ModInv(n, MODULO32);

		public static byte ModInv(byte n) => (byte)ModInv(n, MODULO8);
	}
}
