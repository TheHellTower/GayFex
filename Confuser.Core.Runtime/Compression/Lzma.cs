using System;
using System.IO;
using SevenZip.Compression.LZMA;

namespace Confuser.Core.Runtime.Compression {
	// ReSharper disable once UnusedType.Global
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
			var readCnt = 0;
			while (readCnt < 5) {
				readCnt += s.Read(prop, readCnt, 5 - readCnt);
			}
			decoder.SetDecoderProperties(prop);

			readCnt = 0;
			while (readCnt < sizeof(int)) {
				readCnt += s.Read(prop, readCnt, sizeof(int) - readCnt);
			}

			if (!BitConverter.IsLittleEndian)
				Array.Reverse(prop, 0, sizeof(int));

			var outSize = BitConverter.ToInt32(prop, 0);

			var b = new byte[outSize];
			var z = new MemoryStream(b, true);
			long compressedSize = s.Length - 5 - sizeof(int);
			decoder.Code(s, z, compressedSize, outSize, null);
			return b;
		}
	}
}
