using System.Collections.Generic;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal partial class DynamicMode {
		private sealed class CodeGen : CILCodeGen {
			private readonly Local _block;
			private readonly Local _key;

			public CodeGen(Local block, Local key, ModuleDef module, MethodDef init, IList<Instruction> instructions)
				: base(module, init, instructions) {
				_block = block;
				_key = key;
			}

			protected override Local Var(Variable var) {
				switch (var.Name) {
					case "{BUFFER}":
						return _block;
					case "{KEY}":
						return _key;
					default:
						return base.Var(var);
				}
			}
		}
	}
}