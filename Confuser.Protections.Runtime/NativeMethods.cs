using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Confuser.Runtime {
	[SuppressMessage("ReSharper", "IdentifierTypo")]
	internal static class NativeMethods {
		/// <remarks>https://docs.microsoft.com/en-us/windows/desktop/api/memoryapi/nf-memoryapi-virtualprotect</remarks>
		[DllImport("kernel32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool VirtualProtect(
			IntPtr lpAddress,
			uint dwSize,
			[MarshalAs(UnmanagedType.U4)] MemoryProtection flNewProtect,
			[MarshalAs(UnmanagedType.U4)] out MemoryProtection lpflOldProtect);

		/// <remarks>https://docs.microsoft.com/windows/desktop/api/memoryapi/nf-memoryapi-virtualalloc</remarks>
		[DllImport("kernel32.dll")]
		internal static extern IntPtr VirtualAlloc(
			IntPtr lpAddress, 
			uint dwSize, 
			[MarshalAs(UnmanagedType.U4)] AllocationType flAllocType, 
			[MarshalAs(UnmanagedType.U4)] MemoryProtection flProtect);
		
		/// <remarks>https://docs.microsoft.com/en-us/windows/desktop/api/memoryapi/nf-memoryapi-virtualfree</remarks>
		[DllImport("kernel32.dll")]
		internal static extern bool VirtualFree(
			IntPtr lpAddress, 
			uint dwSize, 
			[MarshalAs(UnmanagedType.U4)] FreeType dwFreeType);

		[DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lib);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern IntPtr GetProcAddress(IntPtr lib, [MarshalAs(UnmanagedType.LPStr)] string proc);
	}
}
