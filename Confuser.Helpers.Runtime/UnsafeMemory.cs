// ReSharper disable UnusedParameter.Global

namespace Confuser {
    /// <summary>
	/// Function collection for unsafe memory operations.
	/// </summary>
	/// <remarks>
	/// These functions require a injection processor to work properly. The processor handling these functions is
	/// enabled by default, so these functions can be used without any issue.
    /// </remarks>
	public static class UnsafeMemory {
#pragma warning disable IDE0060 // Remove unused parameters.
#pragma warning disable CA1801 // Review unused parameters
		/// <summary>
		/// Copies bytes from the source address to the destination address.
		/// </summary>
		/// <param name="destination">The destination address to copy to.</param>
		/// <param name="source">The source address to copy from.</param>
		/// <param name="byteCount">The number of bytes to copy.</param>
		/// <remarks>
		/// The real implementation of this method is handled by the injection class.
		/// The reason for this is that the implementation required for this method can't be expressed using
		/// C#. The injection will strip the call to this method and replace it with the copy block IL
		/// instruction.
		/// </remarks>
		public static void CopyBlock(ref byte destination, ref byte source, uint byteCount) { }

		/// <summary>
		/// Copies bytes from the source address to the destination address.
		/// </summary>
		/// <param name="destination">The destination address to copy to.</param>
		/// <param name="source">The source address to copy from.</param>
		/// <param name="byteCount">The number of bytes to copy.</param>
		/// <remarks>
		/// The real implementation of this method is handled by the injection class.
		/// The reason for this is that the implementation required for this method can't be expressed using
		/// C#. The injection will strip the call to this method and replace it with the copy block IL
		/// instruction.
		/// </remarks>
		public static unsafe void CopyBlock(void* destination, void* source, uint byteCount) { }

		/// <summary>
		/// Initializes a block of memory at the given location with a given initial value.
		/// </summary>
		/// <param name="startAddress">The address of the start of the memory block to initialize.</param>
		/// <param name="value">The value to initialize the block to.</param>
		/// <param name="byteCount">The number of bytes to initialize.</param>
		/// <remarks>
		/// The real implementation of this method is handled by the injection class.
		/// The reason for this is that the implementation required for this method can't be expressed using
		/// C#. The injection will strip the call to this method and replace it with the init block IL
		/// instruction.
		/// </remarks>
		public static unsafe void InitBlock(void* startAddress, byte value, uint byteCount) { }

		/// <summary>
		/// Initializes a block of memory at the given location with a given initial value.
		/// </summary>
		/// <param name="startAddress">The address of the start of the memory block to initialize.</param>
		/// <param name="value">The value to initialize the block to.</param>
		/// <param name="byteCount">The number of bytes to initialize.</param>
		/// <remarks>
		/// The real implementation of this method is handled by the injection class.
		/// The reason for this is that the implementation required for this method can't be expressed using
		/// C#. The injection will strip the call to this method and replace it with the init block IL
		/// instruction.
		/// </remarks>
		public static void InitBlock(ref byte startAddress, byte value, uint byteCount) { }
#pragma warning restore CA1801 // Review unused parameters
#pragma warning restore IDE0060 //  Remove unused parameters.
	}
}
