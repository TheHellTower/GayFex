using System.Runtime.CompilerServices;
using Confuser;

// ReSharper disable once CheckNamespace
namespace K4os.Compression.LZ4.Internal {
	/// <summary>
	/// Memory helper for the LZ4 compression
	/// </summary>
	/// <remarks>
	/// The original implementation of this class can be found here:
	/// https://github.com/MiloszKrajewski/K4os.Compression.LZ4/blob/1.1.11/src/K4os.Compression.LZ4/Internal/Mem.cs
	/// This implementation was rewritten to match better with the runtime environment of ConfuserEx.
	/// </remarks>
	internal static unsafe class Mem {
		internal static void Copy(byte* target, byte* source, int length) => 
			UnsafeMemory.CopyBlock(target, source, (uint)length);

		internal static void Move(byte* target, byte* source, int length) => 
			UnsafeMemory.CopyBlock(target, source, (uint)length);

		#if !NET20 && !NET40
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		#endif
		internal static void WildCopy(byte* target, byte* source, void* limit)
		{
			do
			{
				*(ulong*) target = *(ulong*) source;
				target += sizeof(ulong);
				source += sizeof(ulong);
			}
			while (target < limit);
		}

		internal static void Copy8(byte* target, byte* source)=>
			UnsafeMemory.CopyBlock(target, source, 8);

		internal static void Copy16(byte* target, byte* source) =>
			UnsafeMemory.CopyBlock(target, source, 16);

		internal static void Copy18(byte* target, byte* source) =>
			UnsafeMemory.CopyBlock(target, source, 18);

		internal static ushort Peek16(void* p) => *(ushort*) p;

		internal static void Poke32(void* p, uint v) => *(uint*) p = v;
	}
}
