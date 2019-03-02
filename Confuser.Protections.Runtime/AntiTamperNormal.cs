using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Confuser.Runtime {
	internal static class AntiTamperNormal {
		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		internal static void Initialize() => DecryptSections(typeof(AntiTamperNormal).Module);

		// ReSharper disable once UnusedMember.Global
		/// <remarks>
		/// This method is invoked from the module initializer. The reference is build during injection.
		/// </remarks>
		internal static unsafe IntPtr DecryptSections(Module module) {
			string moduleName = module.FullyQualifiedName;
			bool usePhysical = moduleName.Length > 0 && moduleName[0] == '<';
			var startOfModulePtr = (byte*)Marshal.GetHINSTANCE(module);
			var byteCursor = startOfModulePtr + *(uint*)(startOfModulePtr + 0x3c); // IMAGE_DOS_HEADER (e_lfanew)
			ushort sections = *(ushort*)(byteCursor + 0x6); // IMAGE_FILE_HEADER (NumberOfSections)
			ushort optSize = *(ushort*)(byteCursor + 0x14); // IMAGE_FILE_HEADER (SizeOfOptionalHeader)

			uint* encPos = null; // Start address of the sensitive data section
			uint encSize = 0; // Size of the sensitive data section
			var uintCursor = (uint*)(byteCursor + 0x18 + optSize); // Move to start of first IMAGE_SECTION_HEADER
			uint z = (uint)Mutation.KeyI1, x = (uint)Mutation.KeyI2, c = (uint)Mutation.KeyI3, v = (uint)Mutation.KeyI4;
			for (int i = 0; i < sections; i++) {
				uint nameHash = (*uintCursor++) * (*uintCursor++);
				if (nameHash == (uint)Mutation.KeyI0) {
					// This is the sensitive section that needs to be decrypted.
					// Read the SizeOfRawData and PointerToRawData if "usePhysical" is set
					// Read the VirtualSize and VirtualAddress if "usePhysical" is not set
					encPos = (uint*)(startOfModulePtr + (usePhysical ? *(uintCursor + 3) : *(uintCursor + 1)));
					encSize = (usePhysical ? *(uintCursor + 2) : *(uintCursor + 0)) >> 2;
				}
				else if (nameHash != 0) {
					// Read the location and the size of the section
					var secLoc = (uint*)(startOfModulePtr + (usePhysical ? *(uintCursor + 3) : *(uintCursor + 1)));
					uint secSize = *(uintCursor + 2) >> 2;

					// Transform the key by the raw data of the section (has to match Hash Function in Normal Mode)
					for (uint k = 0; k < secSize; k++) {
						uint t = (z ^ (*secLoc++)) + x + c * v;
						z = x;
						x = c;
						c = v;
						v = t;
					}
				}

				uintCursor += 8;
			}

			// Decrypt the key
			uint[] y = new uint[0x10], d = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				y[i] = v;
				d[i] = x;
				z = (x >> 5) | (x << 27);
				x = (c >> 3) | (c << 29);
				c = (v >> 7) | (v << 25);
				v = (z >> 11) | (z << 21);
			}

			Mutation.Crypt(y, d);

			// Request access to the memory section so it can be modified
			// (normally parts of the program code aren't writable)
			uint protectionOption = MemoryProtectionConstants.PAGE_EXECUTE_READWRITE;
			if (!NativeMethods.VirtualProtect((IntPtr)encPos, encSize << 2, protectionOption, out protectionOption)) {
				// Changing the access to the memory page was rejected for some reason.
				// Maybe someone tampered with the assembly and the key was not decoded correctly anymore.
				// Nothing more to do here.
				return IntPtr.Zero;
			}

			// The previous protection option was already set to execute, read, write.
			// The decryption is either already done or something went wrong.
			if (protectionOption == MemoryProtectionConstants.PAGE_EXECUTE_READWRITE)
				return IntPtr.Zero;

			// Now transform the memory with the decoded key so the method bodies become visible.
			uint h = 0;
			var result = (IntPtr)encPos;
			for (uint i = 0; i < encSize; i++) {
				*encPos ^= y[h & 0xf];
				y[h & 0xf] = (y[h & 0xf] ^ (*encPos++)) + 0x3dbb2819;
				h++;
			}

			return result;
		}
	}
}
