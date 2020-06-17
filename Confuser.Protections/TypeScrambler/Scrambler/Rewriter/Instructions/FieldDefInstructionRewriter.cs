using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
	internal sealed class FieldDefInstructionRewriter : InstructionRewriter<FieldDef> {
		internal override void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body, ref int index, FieldDef operand) {
			if (body[index].OpCode != OpCodes.Ldsfld && body[index].OpCode != OpCodes.Ldsflda && body[index].OpCode != OpCodes.Stsfld) 
				return;

			var declType = service.GetItem(operand.DeclaringType);
			if (declType?.IsScambled == true) {
				body[index].Operand = new MemberRefUser(operand.Module, operand.Name, operand.FieldSig,
					declType.CreateGenericTypeSig(service.GetItem(method.DeclaringType)).ToTypeDefOrRef());
			}
		}
	}
}
