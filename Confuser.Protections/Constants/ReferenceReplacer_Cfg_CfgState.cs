namespace Confuser.Protections.Constants {
	internal static partial class ReferenceReplacer {
		private struct CfgState {
			private uint A;
			private uint B;
			private uint C;
			private uint D;

			public CfgState(uint seed) {
				A = seed *= 0x21412321;
				B = seed *= 0x21412321;
				C = seed *= 0x21412321;
				D = seed *= 0x21412321;
			}

			public void UpdateExplicit(int id, uint value) {
				switch (id) {
					case 0:
						A = value;
						break;
					case 1:
						B = value;
						break;
					case 2:
						C = value;
						break;
					case 3:
						D = value;
						break;
					default:
						throw new UnreachableException();
				}
			}

			public void UpdateIncremental(int id, uint value) {
				switch (id) {
					case 0:
						A *= value;
						break;
					case 1:
						B += value;
						break;
					case 2:
						C ^= value;
						break;
					case 3:
						D -= value;
						break;
					default:
						throw new UnreachableException();
				}
			}

			public uint GetIncrementalUpdate(int id, uint target) {
				switch (id) {
					case 0:
						return A ^ target;
					case 1:
						return target - B;
					case 2:
						return C ^ target;
					case 3:
						return D - target;
					default:
						throw new UnreachableException();
				}
			}

			public uint Get(int id) {
				switch (id) {
					case 0:
						return A;
					case 1:
						return B;
					case 2:
						return C;
					case 3:
						return D;
					default:
						throw new UnreachableException();
				}
			}

			public static byte EncodeFlag(bool exp, int updateId, int getId) {
				byte fl = (byte)(exp ? 0x80 : 0);
				fl |= (byte)updateId;
				fl |= (byte)(getId << 2);
				return fl;
			}
		}
	}
}