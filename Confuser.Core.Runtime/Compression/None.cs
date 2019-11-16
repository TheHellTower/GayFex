using System.Runtime.CompilerServices;

namespace Confuser.Core.Runtime.Compression {
	// ReSharper disable once UnusedType.Global
	/// <remarks>
	/// This class is injected into the code of the assembly to project. The reference is build during injection.
	/// </remarks>
	internal static class None {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
#if !NET20 && !NET40
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static byte[] Decompress(byte[] data) => data;
	}
}
