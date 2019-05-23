using System.Collections.Generic;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy
{
	internal sealed partial class StrongMode {
		private sealed class CodeGen : CILCodeGen {
			private readonly IReadOnlyList<Instruction> arg;

			internal CodeGen(IReadOnlyList<Instruction> arg, ModuleDef module, MethodDef method,
				IList<Instruction> instrs)
				: base(module, method, instrs) {
				this.arg = arg;
			}

			protected override void LoadVar(Variable var) {
				if (var.Name == "{RESULT}") {
					foreach (var instr in arg)
						Emit(instr);
				}
				else
					base.LoadVar(var);
			}
		}
	}
}
