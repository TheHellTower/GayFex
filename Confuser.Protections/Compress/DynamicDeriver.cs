using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.DynCipher;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.Compress {
	internal sealed class DynamicDeriver : IKeyDeriver {
		StatementBlock derivation;
		Action<uint[], uint[]> encryptFunc;

		public void Init(IConfuserContext ctx, IRandomGenerator random) {
			ctx.Registry.GetRequiredService<IDynCipherService>()
				.GenerateCipherPair(random, out derivation, out var dummy);

			var dmCodeGen = new DMCodeGen(typeof(void), new[] {
				Tuple.Create("{BUFFER}", typeof(uint[])),
				Tuple.Create("{KEY}", typeof(uint[]))
			});
			dmCodeGen.GenerateCIL(derivation);
			encryptFunc = dmCodeGen.Compile<Action<uint[], uint[]>>();
		}

		void IKeyDeriver.DeriveKey(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> key) {
			Debug.Assert(a.Length == 0x10, $"{nameof(a)}.Length == 0x10");
			Debug.Assert(b.Length == 0x10, $"{nameof(b)}.Length == 0x10");
			Debug.Assert(key.Length == 0x10, $"{nameof(key)}.Length == 0x10");

			var tmp = new uint[0x10];
			a.CopyTo(tmp);
			encryptFunc(tmp, b.ToArray());
			tmp.CopyTo(key);
		}

		CryptProcessor IKeyDeriver.EmitDerivation(IConfuserContext ctx) => (module, method, block, key) => {
			var ret = new List<Instruction>();
			var codeGen = new CodeGen(block, key, module, method, ret);
			codeGen.GenerateCIL(derivation);
			codeGen.Commit(method.Body);
			return ret;
		};

		private sealed class CodeGen : CILCodeGen {
			private readonly Local block;
			private readonly Local key;

			internal CodeGen(Local block, Local key, ModuleDef module, MethodDef method, IList<Instruction> instrs)
				: base(module, method, instrs) {
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
