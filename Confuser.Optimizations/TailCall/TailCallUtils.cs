using System;
using System.Diagnostics;
using System.Linq;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.TailCall {
	internal static class TailCallUtils {
		internal static bool IsTailCall(MethodDef method, int i) {
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (i < 0) throw new ArgumentOutOfRangeException(nameof(i), i, "Index must not be less than 0");

			Debug.Assert(method.HasBody, $"{nameof(method)}.HasBody");
			Debug.Assert(method.Body.HasInstructions, $"{nameof(method)}.Body.HasInstructions");
			if (!method.HasBody || !method.Body.HasInstructions) return false;

			var instructions = method.Body.Instructions;
			var instructionCount = instructions.Count;
			Debug.Assert(i < instructionCount, $"{nameof(i)} > {nameof(instructionCount)}");

			var instruction = instructions[i];

			if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Calli || instruction.OpCode == OpCodes.Callvirt) {
				// A call operation is a candidate for the tail call optimization.

				// There are no following instructions. There is something strange going on here.
				// Lets stay away from it.
				if (i + 1 == instructionCount) return false;

				// The tail recursion looks differently in case of a debug and a optimized build.
				// Optimized Build:
				// IL_0000: Call    Method()
				// IL_0005: Ret

				// Debug Build:
				// IL_0000: Call    Method()
				// IL_0005: Stloc.1
				// IL_0006: Br.s    IL_0008
				// IL_0008: Ldloc.1
				// IL_0009: Ret

				// Lets first check for the optimized build. It's easier. ;-)
				var nextInstruction = instructions[i + 1];
				if (nextInstruction.OpCode == OpCodes.Ret) return IsCompatibleCall(method, i);

				// So it's not a optimized build. Maybe a debug build.
				if (nextInstruction.OpCode == OpCodes.Stloc) {
					if (i + 4 >= instructionCount) return false;

					var debugVariable = nextInstruction.Operand;
					var branchInstruction = instructions[i + 2];
					if (branchInstruction.OpCode != OpCodes.Br || !(branchInstruction.Operand is Instruction loadDebugVarInstruction))
						return false;

					if (loadDebugVarInstruction.OpCode != OpCodes.Ldloc || loadDebugVarInstruction.Operand != debugVariable)
						return false;

					var loadDebugIndex = instructions.IndexOf(loadDebugVarInstruction);
					if (i + 1 >= instructionCount) return false;

					return instructions[loadDebugIndex + 1].OpCode == OpCodes.Ret && IsCompatibleCall(method, i);
				}
			}

			return false;
		}

		private static bool IsCompatibleCall(MethodDef method, int i) {
			var instruction = method.Body.Instructions[i];

			// Methods that involve some reference stuff won't be optimized, because that just breaks all things.
			var targetMethod = (instruction.Operand as IMethod);
			if (targetMethod == null) {
				Debug.Fail("Call instruction does not point to a method?!");
				return false;
			}

			if (targetMethod.MethodSig.Params.Where(p => p.IsByRef).Any())
				return false;

			var voidType = method.Module.CorLibTypes.Void;
			var typeCmp = TypeEqualityComparer.Instance;
			if (typeCmp.Equals(targetMethod.MethodSig.RetType, voidType) && !typeCmp.Equals(method.ReturnType, voidType))
				return false;

			return true;
		}

		internal static void RemoveUnreachableInstructions(MethodDef method) {
			var instructions = method.Body.Instructions;
			var instructionCount = instructions.Count;
			var checkForDeadCode = true;
			while (checkForDeadCode) {
				// Especially in debug builds, the tail call optimization may produce dead code.
				// To fix this this function will strip the instructions after a return until it finds one that is
				// the target of a branch instruction.
				// This is done in a loop because the operation needs to be repeated in case a branch instruction
				// is removed.
				checkForDeadCode = false;
				for (var i = 0; i < instructionCount; i++) {
					if (instructions[i].OpCode == OpCodes.Ret || instructions[i].OpCode == OpCodes.Br) {
						for (i++; i < instructionCount; i++) {
							var nextInstruction = instructions[i];
							if (!instructions.Any(instr => IsBranchTo(instr, nextInstruction))) {
								method.Body.RemoveInstruction(nextInstruction);
								i--;
								instructionCount--;
								if (!checkForDeadCode && nextInstruction.Operand is Instruction)
									checkForDeadCode = true;
							}
							else {
								break;
							}
						}
					}
				}

				if (!checkForDeadCode) {
					// Due to the stripping of dead code it may be possible that there are local variables not
					// used anymore. Those need to be cleared out as well.
					// We'll only do that on that last loop.
					var variables = method.Body.Variables;
					for (var localI = 0; localI < variables.Count; localI++) {
						var variable = variables[localI];
						if (!instructions.Any(i => i.Operand == variable)) {
							variables.RemoveAt(localI);
							localI--;
						}
					}
				}

			}
		}

		private static bool IsBranchTo(Instruction testInstr, Instruction targetInstr) {
			if (testInstr.Operand is Instruction testInstrTarget)
				return targetInstr == testInstrTarget;

			if (testInstr.Operand is Instruction[] branchTargetInstrs)
				foreach (var branchTargetInstr in branchTargetInstrs)
					if (targetInstr == branchTargetInstr)
						return true;

			return false;
		}
	}
}
