using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Core.Helpers {
	public static class Generator {
		private static Random Random = new Random();
		public static int RandomInteger(int min = 0, int max = 255) {
			lock (Random) {
				return Random.Next(min, max);
			}
		}
		public static string RandomString(int length) {
			lock (Random) {
				return new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", length).Select(s => s[Random.Next(s.Length)]).ToArray());
			}
		}
	}
}
