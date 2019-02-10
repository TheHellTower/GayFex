using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Confuser.Runtime {
	[SuppressMessage("ReSharper", "IdentifierTypo")]
	internal static class NativeMethods {
		[DllImport("kernel32.dll")]
		internal static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect,
			out uint lpflOldProtect);

		[DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode)]
		internal static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lib);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern IntPtr GetProcAddress(IntPtr lib, [MarshalAs(UnmanagedType.LPStr)] string proc);
	}
}
