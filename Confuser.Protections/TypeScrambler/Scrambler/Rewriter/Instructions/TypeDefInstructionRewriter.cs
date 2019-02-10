using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
	class TypeDefInstructionRewriter : InstructionRewriter<TypeDef> {
		public override void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body,
			ref int index, TypeDef operand) {
			ScannedItem t = service.GetItem(operand.MDToken);
			if (t == null) {
				return;
			}

			body[index].Operand =
				new TypeSpecUser(t.CreateGenericTypeSig(service.GetItem(method.DeclaringType.MDToken)));
		}
	}
}
