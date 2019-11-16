using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Confuser.Core.Runtime.Compression {
	// ReSharper disable once UnusedType.Global
	/// <remarks>
	/// This class is injected into the code of the assembly to project. The reference is build during injection.
	/// </remarks>
	internal static class Deflate {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		public static byte[] Decompress(byte[] data) {
			using (var inputStream = new MemoryStream(data, false)) {
				var resultSize = 0;
				for (var i = 0; i < 4; i++) {
					var v = (byte)inputStream.ReadByte();
					resultSize |= v << (8 * i);
				}

				var result = new byte[resultSize];
				using (var outputStream = new MemoryStream(result, true))
				using (var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress, false))
					CopyTo(deflateStream, outputStream);

				return result;
			}
		}

#if NET20
		private static void CopyTo(Stream source, Stream destination) =>
			CopyTo(source, destination, 81920);

		private static void CopyTo(Stream source, Stream destination, int bufferSize) {
			var buffer = new byte[bufferSize];
			int read;
			while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
				destination.Write(buffer, 0, read);
		}
#else
#if !NET40
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		private static void CopyTo(Stream source, Stream destination) =>
			source.CopyTo(destination);
#endif
	}
}
