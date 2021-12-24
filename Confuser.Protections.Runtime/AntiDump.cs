using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Memory;

namespace Confuser.Runtime {
	// ReSharper disable once UnusedMember.Global
	/// <remarks>
	/// This method is invoked from the module initializer. The reference is build during injection.
	/// </remarks>
	internal static class AntiDump {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		private static unsafe void Initialize() {
			var module = typeof(AntiDump).Module;
			var bas = (byte*)Marshal.GetHINSTANCE(module);
			var ptr = bas + 0x3c;
			ptr = bas + *(uint*)ptr;
			ptr += 0x6;
			ushort sectNum = *(ushort*)ptr;
			ptr += 14;
			ushort optSize = *(ushort*)ptr;
			ptr = ptr + 0x4 + optSize;

			var @new = stackalloc byte[11];
			if (module.FullyQualifiedName[0] != '<') //Mapped
			{
				//VirtualProtect(ptr - 16, 8, 0x40, out old);
				//*(uint*)(ptr - 12) = 0;
				var mdDir = bas + *(uint*)(ptr - 16);
				//*(uint*)(ptr - 16) = 0;

				if (*(uint*)(ptr - 0x78) != 0) {
					var importDir = bas + *(uint*)(ptr - 0x78);
					var oftMod = bas + *(uint*)importDir;
					var modName = bas + *(uint*)(importDir + 12);
					var funcName = bas + *(uint*)oftMod + 2;
					PInvoke.VirtualProtect(modName, 11, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);

					*(uint*)@new = 0x6c64746e;
					*((uint*)@new + 1) = 0x6c642e6c;
					*((ushort*)@new + 4) = 0x006c;
					*(@new + 10) = 0;

					UnsafeMemory.CopyBlock(modName, @new, 11);

					PInvoke.VirtualProtect(funcName, 11, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);

					*(uint*)@new = 0x6f43744e;
					*((uint*)@new + 1) = 0x6e69746e;
					*((ushort*)@new + 4) = 0x6575;
					*(@new + 10) = 0;
					
					UnsafeMemory.CopyBlock(funcName, @new, 11);
				}

				for (int i = 0; i < sectNum; i++) {
					PInvoke.VirtualProtect(ptr, 8, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
					UnsafeMemory.InitBlock(ptr, 0, 8);
					ptr += 0x28;
				}

				PInvoke.VirtualProtect(mdDir, 0x48, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
				var mdHdr = bas + *(uint*)(mdDir + 8);

				UnsafeMemory.InitBlock(mdDir, 0, 16);

				PInvoke.VirtualProtect(mdHdr, 4, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
				*(uint*)mdHdr = 0;
				mdHdr += 12;
				mdHdr += *(uint*)mdHdr;
				mdHdr = (byte*)(((ulong)mdHdr + 7) & ~3UL);
				mdHdr += 2;
				ushort numOfStream = *mdHdr;
				mdHdr += 2;
				for (int i = 0; i < numOfStream; i++) {
					PInvoke.VirtualProtect(mdHdr, 8, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
					//*(uint*)mdHdr = 0;
					mdHdr += 4;
					//*(uint*)mdHdr = 0;
					mdHdr += 4;
					for (int ii = 0; ii < 8; ii++) {
						PInvoke.VirtualProtect(mdHdr, 4, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
						*mdHdr = 0;
						mdHdr++;
						if (*mdHdr == 0) {
							mdHdr += 3;
							break;
						}

						*mdHdr = 0;
						mdHdr++;
						if (*mdHdr == 0) {
							mdHdr += 2;
							break;
						}

						*mdHdr = 0;
						mdHdr++;
						if (*mdHdr == 0) {
							mdHdr += 1;
							break;
						}

						*mdHdr = 0;
						mdHdr++;
					}
				}
			}
			else //Flat
			{
				//VirtualProtect(ptr - 16, 8, 0x40, out old);
				//*(uint*)(ptr - 12) = 0;
				uint mdDir = *(uint*)(ptr - 16);
				//*(uint*)(ptr - 16) = 0;
				uint importDir = *(uint*)(ptr - 0x78);

				var vAdrs = new uint[sectNum];
				var vSizes = new uint[sectNum];
				var rAdrs = new uint[sectNum];
				for (int i = 0; i < sectNum; i++) {
					PInvoke.VirtualProtect(ptr, 8, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
					Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
					vAdrs[i] = *(uint*)(ptr + 12);
					vSizes[i] = *(uint*)(ptr + 8);
					rAdrs[i] = *(uint*)(ptr + 20);
					ptr += 0x28;
				}


				if (importDir != 0) {
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= importDir && importDir < vAdrs[i] + vSizes[i]) {
							importDir = importDir - vAdrs[i] + rAdrs[i];
							break;
						}

					var importDirPtr = bas + importDir;
					uint oftMod = *(uint*)importDirPtr;
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= oftMod && oftMod < vAdrs[i] + vSizes[i]) {
							oftMod = oftMod - vAdrs[i] + rAdrs[i];
							break;
						}

					var oftModPtr = bas + oftMod;
					uint modName = *(uint*)(importDirPtr + 12);
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= modName && modName < vAdrs[i] + vSizes[i]) {
							modName = modName - vAdrs[i] + rAdrs[i];
							break;
						}

					uint funcName = *(uint*)oftModPtr + 2;
					for (int i = 0; i < sectNum; i++)
						if (vAdrs[i] <= funcName && funcName < vAdrs[i] + vSizes[i]) {
							funcName = funcName - vAdrs[i] + rAdrs[i];
							break;
						}

					PInvoke.VirtualProtect(bas + modName, 11, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);

					*(uint*)@new = 0x6c64746e;
					*((uint*)@new + 1) = 0x6c642e6c;
					*((ushort*)@new + 4) = 0x006c;
					*(@new + 10) = 0;

					UnsafeMemory.CopyBlock(bas + modName, @new, 11);

					PInvoke.VirtualProtect(bas + funcName, 11, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);

					*(uint*)@new = 0x6f43744e;
					*((uint*)@new + 1) = 0x6e69746e;
					*((ushort*)@new + 4) = 0x6575;
					*(@new + 10) = 0;
					
					UnsafeMemory.CopyBlock(bas + funcName, @new, 11);
				}


				for (int i = 0; i < sectNum; i++)
					if (vAdrs[i] <= mdDir && mdDir < vAdrs[i] + vSizes[i]) {
						mdDir = mdDir - vAdrs[i] + rAdrs[i];
						break;
					}

				var mdDirPtr = bas + mdDir;
				PInvoke.VirtualProtect(mdDirPtr, 0x48, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
				uint mdHdr = *(uint*)(mdDirPtr + 8);
				for (int i = 0; i < sectNum; i++)
					if (vAdrs[i] <= mdHdr && mdHdr < vAdrs[i] + vSizes[i]) {
						mdHdr = mdHdr - vAdrs[i] + rAdrs[i];
						break;
					}

				UnsafeMemory.InitBlock(mdDirPtr, 0, 16);

				var mdHdrPtr = bas + mdHdr;
				PInvoke.VirtualProtect(mdHdrPtr, 4, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
				*(uint*)mdHdrPtr = 0;
				mdHdrPtr += 12;
				mdHdrPtr += *(uint*)mdHdrPtr;
				mdHdrPtr = (byte*)(((ulong)mdHdrPtr + 7) & ~3UL);
				mdHdrPtr += 2;
				ushort numOfStream = *mdHdrPtr;
				mdHdrPtr += 2;
				for (int i = 0; i < numOfStream; i++) {
					PInvoke.VirtualProtect(mdHdrPtr, 8, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
					//*(uint*)mdHdrPtr = 0;
					mdHdrPtr += 4;
					//*(uint*)mdHdrPtr = 0;
					mdHdrPtr += 4;
					for (int ii = 0; ii < 8; ii++) {
						PInvoke.VirtualProtect(mdHdrPtr, 4, PAGE_PROTECTION_FLAGS.PAGE_EXECUTE_READWRITE, out _);
						*mdHdrPtr = 0;
						mdHdrPtr++;
						if (*mdHdrPtr == 0) {
							mdHdrPtr += 3;
							break;
						}

						*mdHdrPtr = 0;
						mdHdrPtr++;
						if (*mdHdrPtr == 0) {
							mdHdrPtr += 2;
							break;
						}

						*mdHdrPtr = 0;
						mdHdrPtr++;
						if (*mdHdrPtr == 0) {
							mdHdrPtr += 1;
							break;
						}

						*mdHdrPtr = 0;
						mdHdrPtr++;
					}
				}
			}
		}
	}
}
