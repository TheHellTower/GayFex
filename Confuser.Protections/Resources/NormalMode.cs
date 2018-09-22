using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Helpers;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Resources {
	internal sealed class NormalMode : IEncodeMode {
		CryptProcessor IEncodeMode.EmitDecrypt(REContext ctx) => (module, method, block, key) => {
			var result = new List<Instruction>(10 * 0x10);
			for (int i = 0; i < 0x10; i++) {
				result.Add(Instruction.Create(OpCodes.Ldloc, block));
				result.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				result.Add(Instruction.Create(OpCodes.Ldloc, block));
				result.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				result.Add(Instruction.Create(OpCodes.Ldelem_U4));
				result.Add(Instruction.Create(OpCodes.Ldloc, key));
				result.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				result.Add(Instruction.Create(OpCodes.Ldelem_U4));
				result.Add(Instruction.Create(OpCodes.Xor));
				result.Add(Instruction.Create(OpCodes.Stelem_I4));
			}
			return result;
		};

		void IEncodeMode.Encrypt(ReadOnlySpan<uint> data, ReadOnlySpan<uint> key, Span<uint> dest) {
			Debug.Assert(key.Length == dest.Length, $"{nameof(key)}.Length == {nameof(dest)}.Length");

			for (int i = 0; i < key.Length; i++)
				dest[i] = data[i] ^ key[i];
		}
	}
}
