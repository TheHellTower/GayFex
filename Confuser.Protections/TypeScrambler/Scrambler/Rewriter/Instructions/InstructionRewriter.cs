using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
	abstract class InstructionRewriter {
		public abstract void ProcessInstruction(TypeService service, MethodDef method, IList<Instruction> body,
			ref int index, Instruction i);

		public abstract Type TargetType();
	}

	abstract class InstructionRewriter<T> : InstructionRewriter {
		public override void ProcessInstruction(TypeService service, MethodDef method, IList<Instruction> body,
			ref int index, Instruction i) {
			ProcessOperand(service, method, body, ref index, (T)i.Operand);
		}

		public override Type TargetType() => typeof(T);

		public abstract void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body,
			ref int index, T operand);
	}
}
