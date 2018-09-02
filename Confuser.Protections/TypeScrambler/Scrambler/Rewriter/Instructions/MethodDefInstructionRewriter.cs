using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Rewriter.Instructions {
	internal sealed class MethodDefInstructionRewriter : InstructionRewriter<MethodDef> {
		internal override void ProcessOperand(TypeService service, MethodDef method, IList<Instruction> body, ref int index, MethodDef operand) {
			Debug.Assert(service != null, $"{nameof(service)} != null");
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(body != null, $"{nameof(body)} != null");
			Debug.Assert(operand != null, $"{nameof(operand)} != null");
			Debug.Assert(index >= 0, $"{nameof(index)} >= 0");
			Debug.Assert(index < body.Count, $"{nameof(index)} < {nameof(body)}.Count");

			var targetMethod = service.GetItem(operand);
			if (targetMethod?.IsScambled == true) {
				var currentItem = service.GetItem(method);
				var newSpec = new MethodSpecUser(targetMethod.TargetMethod, targetMethod.CreateGenericMethodSig(currentItem));

				Debug.Assert(newSpec.GenericInstMethodSig.GenericArguments.Count == targetMethod.TargetMethod.GenericParameters.Count,
					$"{nameof(newSpec)}.GenericInstMethodSig.GenericArguments.Count == {nameof(targetMethod)}.TargetMethod.GenericParameters.Count");

				body[index].Operand = newSpec;
			}
		}
	}
}
