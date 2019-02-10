using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Resources {
	internal class DynamicMode : IEncodeMode {
		private Action<uint[], uint[]> encryptFunc;

		CryptProcessor IEncodeMode.EmitDecrypt(REContext ctx) => (module, init, block, key) => {
			ctx.DynCipher.GenerateCipherPair(ctx.Random, out var encrypt, out var decrypt);
			var ret = new List<Instruction>();

			var codeGen = new CodeGen(block, key, module, init, ret);
			codeGen.GenerateCIL(decrypt);
			codeGen.Commit(init.Body);

			var dmCodeGen = new DMCodeGen(typeof(void), new[] {
				Tuple.Create("{BUFFER}", typeof(uint[])),
				Tuple.Create("{KEY}", typeof(uint[]))
			});
			dmCodeGen.GenerateCIL(encrypt);
			encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();

			return ret;
		};

		void IEncodeMode.Encrypt(ReadOnlySpan<uint> data, ReadOnlySpan<uint> key, Span<uint> dest) {
			Debug.Assert(key.Length == dest.Length, $"{nameof(key)}.Length == {nameof(dest)}.Length");

			var tempDataArray = ArrayPool<uint>.Shared.Rent(key.Length);
			var tempKeyArray = ArrayPool<uint>.Shared.Rent(key.Length);
			try {
				data.Slice(0, key.Length).CopyTo(tempDataArray);
				key.CopyTo(tempKeyArray);
				encryptFunc(tempDataArray, tempKeyArray);

				tempDataArray.AsSpan().Slice(0, key.Length).CopyTo(dest);
			}
			finally {
				ArrayPool<uint>.Shared.Return(tempDataArray);
				ArrayPool<uint>.Shared.Return(tempKeyArray);
			}
		}

		private sealed class CodeGen : CILCodeGen {
			private readonly Local block;
			private readonly Local key;

			public CodeGen(Local block, Local key, ModuleDef module, MethodDef init, IList<Instruction> instrs)
				: base(module, init, instrs) {
				this.block = block;
				this.key = key;
			}

			protected override Local Var(Variable var) {
				switch (var.Name) {
					case "{BUFFER}": return block;
					case "{KEY}": return key;
					default: return base.Var(var);
				}
			}
		}
	}
}
