using System;
using System.Reflection;

namespace Confuser.Runtime {
	internal static class Resource_Shared {
		internal static Assembly InitAssembly() {
			var l = (uint)Mutation.KeyI0;
			uint[] q = Mutation.Placeholder(new uint[Mutation.KeyI0]);

			var k = new uint[0x10];
			var n = (uint)Mutation.KeyI1;
			for (int i = 0; i < 0x10; i++) {
				n ^= n >> 13;
				n ^= n << 25;
				n ^= n >> 27;
				k[i] = n;
			}

			int s = 0, d = 0;
			var w = new uint[0x10];
			var o = new byte[l * 4];
			while (s < l) {
				for (int j = 0; j < 0x10; j++)
					w[j] = q[s + j];
				Mutation.Crypt(w, k);
				for (int j = 0; j < 0x10; j++) {
					uint e = w[j];
					o[d++] = (byte)e;
					o[d++] = (byte)(e >> 8);
					o[d++] = (byte)(e >> 16);
					o[d++] = (byte)(e >> 24);
					k[j] ^= e;
				}
				s += 0x10;
			}

			return Assembly.Load(Lzma.Decompress(o));
		}
	}
	internal static class Resource {
		private static Assembly c;
		
		internal static void Initialize() {
			c = Resource_Shared.InitAssembly();
			AppDomain.CurrentDomain.AssemblyResolve += Handler;
		}

		private static Assembly Handler(object sender, ResolveEventArgs args) {
			if (string.Equals(c.FullName, args.Name, StringComparison.OrdinalIgnoreCase))
				return c;
			return null;
		}
	}

	internal static class Resource_Packer {
		private static Assembly c;

		internal static void Initialize() {
			c = Resource_Shared.InitAssembly();
			AppDomain.CurrentDomain.ResourceResolve += Handler;
		}

		private static Assembly Handler(object sender, ResolveEventArgs args) {
			var n = c.GetManifestResourceNames();
			if (Array.IndexOf(n, args.Name) != -1)
				return c;
			return null;
		}
	}
}
