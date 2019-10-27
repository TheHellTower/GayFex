using System.IO;
using SevenZip.Compression.LZMA;

namespace Confuser.Core.Runtime.Compression {
	// ReSharper disable once UnusedMember.Global
	/// <remarks>
	/// This class is injected into the code of the assembly to project. The reference is build during injection.
	/// </remarks>
	internal static class Lzma {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		public static byte[] Decompress(byte[] data) {
			var s = new MemoryStream(data);
			var decoder = new Decoder();
			var prop = new byte[5];
			s.Read(prop, 0, 5);
			decoder.SetDecoderProperties(prop);
			long outSize = 0;
			for (int i = 0; i < 8; i++) {
				var v = (byte)s.ReadByte();
				outSize |= (long)v << (8 * i);
			}

			var b = new byte[(int)outSize];
			var z = new MemoryStream(b, true);
			long compressedSize = s.Length - 13;
			decoder.Code(s, z, compressedSize, outSize, null);
			return b;
		}
	}
}
