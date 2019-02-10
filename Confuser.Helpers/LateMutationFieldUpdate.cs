using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Helpers {
	public sealed class LateMutationFieldUpdate {
		private IList<(MethodDef Method, Instruction Instruction)> UpdateInstructions { get; } =
			new List<(MethodDef, Instruction)>();

		internal void AddUpdateInstruction(MethodDef method, Instruction instruction) =>
			UpdateInstructions.Add((method, instruction));

		public void ApplyValue(int value) {
			foreach (var methodAndInstruction in UpdateInstructions) {
				var instr = methodAndInstruction.Instruction;
				var method = methodAndInstruction.Method;

				if (method.HasBody && method.Body.HasInstructions) {
					if (method.Body.Instructions.Contains(instr)) {
						instr.OpCode = OpCodes.Ldc_I4;
						instr.Operand = value;
					}
					else {
						Debug.Fail("Instruction is not in method anymore?!");
					}
				}
				else {
					Debug.Fail("Method has no body anymore?!");
				}
			}
		}
	}
}
