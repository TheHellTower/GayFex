using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal sealed class PassThroughMode : IEncodeMode, IEncryptMode {
		public CryptProcessor EmitDecrypt(CEContext ctx) => (module, method, block, key) => {
			var ret = new List<Instruction>(10 * 0x10);
			for (int i = 0; i < 0x10; i++) {
				ret.Add(Instruction.Create(OpCodes.Ldloc, block));
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				ret.Add(Instruction.Create(OpCodes.Ldloc, block));
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				ret.Add(Instruction.Create(OpCodes.Ldelem_U4));
				ret.Add(Instruction.Create(OpCodes.Ldloc, key));
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				ret.Add(Instruction.Create(OpCodes.Ldelem_U4));
				ret.Add(Instruction.Create(OpCodes.Xor));
				ret.Add(Instruction.Create(OpCodes.Stelem_I4));
			}

			return ret;
		};

		public uint[] Encrypt(uint[] data, int offset, uint[] key) {
			var ret = new uint[key.Length];
			Array.Copy(data, offset, ret, 0, ret.Length);
			return ret;
		}

		(PlaceholderProcessor, object) IEncodeMode.CreateDecoder(CEContext ctx) {
			uint k1 = ctx.Random.NextUInt32() | 1;
			uint k2 = ctx.Random.NextUInt32();

			IReadOnlyList<Instruction> processor(ModuleDef module, MethodDef method, IReadOnlyList<Instruction> arg) {
				var repl = new List<Instruction>(arg.Count + 4);
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)MathsUtils.ModInv(k1)));
				repl.Add(Instruction.Create(OpCodes.Mul));
				repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k2));
				repl.Add(Instruction.Create(OpCodes.Xor));
				return repl;
			}

			return (processor, Tuple.Create(k1, k2));
		}

		public uint Encode(object data, CEContext ctx, uint id) {
			var key = (Tuple<uint, uint>)data;
			uint ret = (id ^ key.Item2) * key.Item1;
			Debug.Assert(((ret * MathsUtils.ModInv(key.Item1)) ^ key.Item2) == id);
			return ret;
		}
	}
}
