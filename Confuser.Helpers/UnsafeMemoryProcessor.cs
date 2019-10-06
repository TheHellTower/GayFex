using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Helpers {
	internal sealed class UnsafeMemoryProcessor : IMethodInjectProcessor {
		private const string UnsafeMemoryClassName = "Confuser.UnsafeMemory";

		/// <inheritdoc />
		public void Process(MethodDef method) {
			if (method == null || !method.HasBody || !method.Body.HasInstructions) return;

			var instructions = method.Body.Instructions;
			for (var i = 0; i < instructions.Count; i++) {
				var instr = instructions[i];

				if (instr.OpCode.Code == Code.Call) {
					if (instr.Operand is IMethod calledMethod && calledMethod.DeclaringType.FullName == UnsafeMemoryClassName) {
						if (ReplaceCopyBlock(method, instr, calledMethod) 
						    || ReplaceInitBlock(method, instr, calledMethod)) { }
					}
				}
			}
		}

		private static bool ReplaceCopyBlock(MethodDef method, Instruction instr, IMethod calledMethod) {
			if (method is null) throw new ArgumentNullException(nameof(method));
			if (instr is null) throw new ArgumentNullException(nameof(instr));
			if (calledMethod is null) throw new ArgumentNullException(nameof(calledMethod));

			if (!calledMethod.Name.Equals("CopyBlock")) return false;

			instr.OpCode = OpCodes.Cpblk;
			instr.Operand = null;
			return true;
		}

		private static bool ReplaceInitBlock(MethodDef method, Instruction instr, IMethod calledMethod) {
			if (method is null) throw new ArgumentNullException(nameof(method));
			if (instr is null) throw new ArgumentNullException(nameof(instr));
			if (calledMethod is null) throw new ArgumentNullException(nameof(calledMethod));

			if (!calledMethod.Name.Equals("InitBlock")) return false;

			instr.OpCode = OpCodes.Initblk;
			instr.Operand = null;
			return true;
		}
	}
}
