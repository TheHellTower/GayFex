using System;
using K4os.Compression.LZ4.Engine;

namespace Confuser.Core.Runtime.Compression {
	// ReSharper disable once UnusedMember.Global
	/// <remarks>
	/// This class is injected into the code of the assembly to project. The reference is build during injection.
	/// </remarks>
	internal static class Lz4 {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		public static unsafe byte[] Decompress(byte[] data) {
			var targetLength = BitConverter.ToInt64(data, 0);
			var targetArray = new byte[(int)targetLength];
			fixed(byte *source = data)
				fixed(byte *target = targetArray)
					LZ4_xx.LZ4_decompress_safe(source + 8, target, data.Length - 8, (int)targetLength);
			return targetArray;
		}
	}
}
