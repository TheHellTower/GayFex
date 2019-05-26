using System.IO;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher {
	public static class CodeGenUtils {
		public static byte[] AssembleCode(x86CodeGen codeGen, x86Register reg) {
			var stream = new MemoryStream();
			using (var writer = new BinaryWriter(stream)) {
				/* 
                 *      mov eax, esp
                 *      push ebx
                 *      push edi
                 *      push esi
                 *      sub eax, esp
                 *      cmp eax, 24             ; determine the bitness of platform
                 *      je n
                 *      mov eax, [esp + 4]      ; 32 bits => argument in stack
                 *      push eax
                 *      jmp z
                 *  n:  push ecx                ; 64 bits => argument in register
                 *  z:  XXX
                 *      pop esi
                 *      pop edi
                 *      pop ebx
                 *      pop ret
                 *      
                 */
				writer.Write(new byte[] {0x89, 0xe0});
				writer.Write(new byte[] {0x53});
				writer.Write(new byte[] {0x57});
				writer.Write(new byte[] {0x56});
				writer.Write(new byte[] {0x29, 0xe0});
				writer.Write(new byte[] {0x83, 0xf8, 0x18});
				writer.Write(new byte[] {0x74, 0x07});
				writer.Write(new byte[] {0x8b, 0x44, 0x24, 0x10});
				writer.Write(new byte[] {0x50});
				writer.Write(new byte[] {0xeb, 0x01});
				writer.Write(new byte[] {0x51});

				foreach (var i in codeGen.Instructions)
					writer.Write(i.Assemble());

				if (reg != x86Register.EAX)
					writer.Write(x86Instruction.Create(x86OpCode.MOV, new x86RegisterOperand(x86Register.EAX),
						new x86RegisterOperand(reg)).Assemble());

				writer.Write(new byte[] {0x5e});
				writer.Write(new byte[] {0x5f});
				writer.Write(new byte[] {0x5b});
				writer.Write(new byte[] {0xc3});
			}

			return stream.ToArray();
		}
	}
}