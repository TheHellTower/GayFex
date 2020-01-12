using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal partial class DynamicMode : IEncodeMode {
		private Action<uint[], uint[]> _encryptFunc;

		CryptProcessor IEncodeMode.EmitDecrypt(CEContext ctx) {
			ctx.DynCipher.GenerateCipherPair(ctx.Random, out var encrypt, out var decrypt);

			var dmCodeGen = new DMCodeGen(typeof(void), new[] {
				Tuple.Create("{BUFFER}", typeof(uint[])),
				Tuple.Create("{KEY}", typeof(uint[]))
			});
			dmCodeGen.GenerateCIL(encrypt);
			_encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();

			return (module, method, block, key) => {
				var ret = new List<Instruction>();
				var codeGen = new CodeGen(block, key, module, method, ret);
				codeGen.GenerateCIL(decrypt);
				codeGen.Commit(method.Body);
				return ret;
			};
		}

		public uint[] Encrypt(uint[] data, int offset, uint[] key) {
			var ret = new uint[key.Length];
			Buffer.BlockCopy(data, offset * sizeof(uint), ret, 0, key.Length * sizeof(uint));
			_encryptFunc(ret, key);
			return ret;
		}

		(PlaceholderProcessor, object) IEncodeMode.CreateDecoder(CEContext ctx) {
			uint k1 = ctx.Random.NextUInt32() | 1;
			uint k2 = ctx.Random.NextUInt32();

			IReadOnlyList<Instruction> Processor(ModuleDef module, MethodDef method, IReadOnlyList<Instruction> arg) {
				var replacement = new List<Instruction>(arg.Count + 4);
				replacement.AddRange(arg);
				replacement.Add(Instruction.Create(OpCodes.Ldc_I4, (int)MathsUtils.ModInv(k1)));
				replacement.Add(Instruction.Create(OpCodes.Mul));
				replacement.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k2));
				replacement.Add(Instruction.Create(OpCodes.Xor));
				return replacement.ToArray();
			}

			;
			return (Processor, Tuple.Create(k1, k2));
		}

		public uint Encode(object data, CEContext ctx, uint id) {
			var key = (Tuple<uint, uint>)data;
			uint ret = (id ^ key.Item2) * key.Item1;
			Debug.Assert(((ret * MathsUtils.ModInv(key.Item1)) ^ key.Item2) == id);
			return ret;
		}
	}
}
