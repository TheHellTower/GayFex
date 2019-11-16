using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal static partial class ReferenceReplacer {
		private static bool ReplaceNormal(MethodDef method,
			IEnumerable<(Instruction TargetInstruction, uint Argument, IMethod DecoderMethod)> instrs) {
			foreach (var (targetInstruction, argument, decoderMethod) in instrs)
				ReplaceNormalInstruction(method, targetInstruction, decoderMethod, argument);

			return true;
		}

		private static void ReplaceNormalInstruction(MethodDef method, Instruction targetInstruction, IMethod decoderMethod, uint argument) {
			Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_I4 ||
			             decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Int32);
			Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_I8 ||
			             decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Int64);
			Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_R4 ||
			             decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Single);
			Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_R8 ||
			             decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Double);
			Debug.Assert(targetInstruction.OpCode != OpCodes.Ldstr ||
			             decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.String);
			Debug.Assert(targetInstruction.OpCode != OpCodes.Call ||
			             decoderMethod.GetReturnTypeSig().IsSZArray);

			if (targetInstruction.OpCode.Code == Code.Call)
				ReplaceNormalInitializer(method, targetInstruction, argument, decoderMethod);
			else
				ReplaceNormalOther(method, targetInstruction, argument, decoderMethod);
		}

		private static void ReplaceNormalInitializer(MethodDef method, Instruction targetInstruction, uint argument, IMethod decoderMethod) {
			var methodInstr = method.Body.Instructions;
			int i = methodInstr.IndexOf(targetInstruction);

			Debug.Assert(methodInstr[i - 4].OpCode == OpCodes.Ldc_I4);
			methodInstr[i - 4].Operand = (int)argument;
			methodInstr[i - 3].OpCode = OpCodes.Call;
			methodInstr[i - 3].Operand = decoderMethod;
			method.Body.RemoveInstruction(i - 2);
			method.Body.RemoveInstruction(i - 2);
			method.Body.RemoveInstruction(i - 2);
		}

		private static void ReplaceNormalOther(MethodDef method, Instruction targetInstruction, uint argument, IMethod decoderMethod) {
			int i = method.Body.Instructions.IndexOf(targetInstruction);
			targetInstruction.OpCode = OpCodes.Ldc_I4;
			targetInstruction.Operand = (int)argument;

			method.Body.Instructions.Insert(i + 1, OpCodes.Call.ToInstruction(decoderMethod));
		}

		private static TypeSig GetReturnTypeSig(this IMethod method) {
			if (method.MethodSig.RetType.IsGenericParameter) {
				var genericReturn = (GenericSig)method.MethodSig.RetType;
				var genericMethod = (MethodSpec)method;
				return ((GenericInstMethodSig)genericMethod.Instantiation).GenericArguments[(int)genericReturn.Number];
			}

			return method.MethodSig.RetType;
		}
	}
}