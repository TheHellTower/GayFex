using System;
using K4os.Compression.LZ4.Engine;

namespace Confuser.Core.Runtime.Compression {
	// ReSharper disable once UnusedType.Global
	/// <remarks>
	/// This class is injected into the code of the assembly to project. The reference is build during injection.
	/// </remarks>
	internal static class Lz4 {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		public static unsafe byte[] Decompress(byte[] data) {
			var resultSize = 0;
			for (var i = 0; i < 4; i++) 
				resultSize |= data[i] << (8 * i);

			var compressedSize = 0;
			for (var i = 4; i < 8; i++) 
				compressedSize |= data[i] << (8 * i);

			var targetArray = new byte[resultSize];
			fixed(byte *source = data)
				fixed(byte *target = targetArray)
					LZ4_xx.LZ4_decompress_safe(source + 8, target, compressedSize, resultSize);
			return targetArray;
		}
	}
}
