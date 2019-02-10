using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Constants {
	internal class DynamicMode : IEncodeMode {
		Action<uint[], uint[]> encryptFunc;

		CryptProcessor IEncodeMode.EmitDecrypt(CEContext ctx) {
			StatementBlock encrypt, decrypt;
			ctx.DynCipher.GenerateCipherPair(ctx.Random, out encrypt, out decrypt);

			var dmCodeGen = new DMCodeGen(typeof(void), new[] {
				Tuple.Create("{BUFFER}", typeof(uint[])),
				Tuple.Create("{KEY}", typeof(uint[]))
			});
			dmCodeGen.GenerateCIL(encrypt);
			encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();

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
			encryptFunc(ret, key);
			return ret;
		}

		(PlaceholderProcessor, object) IEncodeMode.CreateDecoder(CEContext ctx) {
			uint k1 = ctx.Random.NextUInt32() | 1;
			uint k2 = ctx.Random.NextUInt32();

			IReadOnlyList<Instruction> processor(ModuleDef module, MethodDef method, IReadOnlyList<Instruction> arg) {
				var repl = new List<Instruction>(arg.Count + 4);
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)MathsUtils.modInv(k1)));
				repl.Add(Instruction.Create(OpCodes.Mul));
				repl.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k2));
				repl.Add(Instruction.Create(OpCodes.Xor));
				return repl.ToArray();
			}

			;
			return (processor, Tuple.Create(k1, k2));
		}

		public uint Encode(object data, CEContext ctx, uint id) {
			var key = (Tuple<uint, uint>)data;
			uint ret = (id ^ key.Item2) * key.Item1;
			Debug.Assert(((ret * MathsUtils.modInv(key.Item1)) ^ key.Item2) == id);
			return ret;
		}

		class CodeGen : CILCodeGen {
			readonly Local block;
			readonly Local key;

			public CodeGen(Local block, Local key, ModuleDef module, MethodDef init, IList<Instruction> instrs)
				: base(module, init, instrs) {
				this.block = block;
				this.key = key;
			}

			protected override Local Var(Variable var) {
				if (var.Name == "{BUFFER}")
					return block;
				if (var.Name == "{KEY}")
					return key;
				return base.Var(var);
			}
		}
	}
}
