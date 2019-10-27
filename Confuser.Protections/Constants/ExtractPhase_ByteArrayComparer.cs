using System;
using System.Collections.Generic;
using System.Linq;

namespace Confuser.Protections.Constants {
	internal sealed partial class ExtractPhase {
		[Serializable]
		private sealed class ByteArrayComparer : IEqualityComparer<byte[]> {
			public bool Equals(byte[] x, byte[] y) {
				if (x is null && y is null) return true;
				if (x is null || y is null) return false;
				return x.SequenceEqual(y);
			}

			public int GetHashCode(byte[] obj) =>
				obj.Aggregate(31, (current, v) => current * 17 + v);
		}
	}
}