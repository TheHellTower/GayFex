using System.IO;
using SevenZip.Compression.LZMA;

namespace Confuser.Core.Runtime {
	internal static partial class Lzma {
		public static byte[] Decompress(byte[] data) {
			var s = new MemoryStream(data);
			var decoder = new Decoder();
			var prop = new byte[5];
			s.Read(prop, 0, 5);
			decoder.SetDecoderProperties(prop);
			long outSize = 0;
			for (int i = 0; i < 8; i++) {
				int v = s.ReadByte();
				outSize |= ((long)(byte)v) << (8 * i);
			}

			var b = new byte[(int)outSize];
			var z = new MemoryStream(b, true);
			long compressedSize = s.Length - 13;
			decoder.Code(s, z, compressedSize, outSize, null);
			return b;
		}
	}
}
