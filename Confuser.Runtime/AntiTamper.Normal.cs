using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Confuser.Runtime {
	internal static class AntiTamperNormal {
		[DllImport("kernel32.dll")]
		internal static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

		[DllImport("kernel32.dll")]
		internal static unsafe extern bool VirtualProtect(void* lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

		[DllImport("kernel32.dll")]
		internal static extern IntPtr ZeroMemory(IntPtr addr, IntPtr size);

		[DllImport("kernel32.dll")]
		internal static extern bool SwitchToThread();

		static Module GetTypeModule() {
			SwitchToThread();

			return typeof(AntiTamperNormal).Module;
		}

		static unsafe void Initialize(string zyx) {

			SwitchToThread();
			Module m = GetTypeModule();
			string n = m.FullyQualifiedName;
			bool f = n.Length > 0 && n[0] == '<';
			var M = typeof(Marshal);
			var GHI = M.GetMethod(string.Join(string.Empty, new string[] { "G", "e", "t", "H", "I", "N", "S", "T", "A", "N", "C", "E" }), new Type[] { m.GetType() });
			byte* b = (byte*)0;
			if (!(GHI is null))
				b = (byte*)(IntPtr)GHI.Invoke(null, new object[] { m });

			byte* p = b + *(uint*)(b + 0x3c);
			ushort s = *(ushort*)(p + 0x6);
			ushort o = *(ushort*)(p + 0x14);

			uint* e = null;
			uint l = 0;
			var r = (uint*)(p + 0x18 + o);
			uint z = (uint)Mutation.KeyI1, x = (uint)Mutation.KeyI2, c = (uint)Mutation.KeyI3, v = (uint)Mutation.KeyI4;

			IntPtr FunnyPart = (IntPtr)((void*)(p + 24));
			uint prot = 0;
			VirtualProtect((void*)FunnyPart, 1U, 64U, out prot);
			ZeroMemory(FunnyPart, (IntPtr)1);
			ZeroMemory(FunnyPart, (IntPtr)2);
			VirtualProtect((void*)FunnyPart, 1U, prot, out prot);

			for (int i = 0; i < s; i++) {
				uint g = (*r++) * (*r++);
				if (g == (uint)Mutation.KeyI0) {
					e = (uint*)(b + (f ? *(r + 3) : *(r + 1)));
					l = (f ? *(r + 2) : *(r + 0)) >> 2;
				}
				else if (g != 0) {
					var q = (uint*)(b + (f ? *(r + 3) : *(r + 1)));
					uint j = *(r + 2) >> 2;
					for (uint k = 0; k < j; k++) {
						uint t = (z ^ (*q++)) + x + c * v;
						z = x;
						x = c;
						x = v;
						v = t;
					}
				}
				r += 8;
			}

			uint[] y = new uint[0x10], d = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				y[i] = v;
				d[i] = x;
				z = (x >> 5) | (x << 27);
				x = (c >> 3) | (c << 29);
				c = (v >> 7) | (v << 25);
				v = (z >> 11) | (z << 21);
			}
			Mutation.Crypt(y, d);

			uint w = 0x40;
			VirtualProtect((IntPtr)e, l << 2, w, out w);

			if (w == 0x40)
				return;

			uint h = 0;
			for (uint i = 0; i < l; i++) {
				*e ^= y[h & 0xf];
				y[h & 0xf] = (y[h & 0xf] ^ (*e++)) + 0x3dbb2819;
				h++;
			}
		}
	}
}
