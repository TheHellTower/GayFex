using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal static class SelfProtection {
		internal static List<Instruction> InstructionsToMutate = new List<Instruction>();

		//TODO: Implement all of them.
		private static IMethod _absIntMethod, _minIntMethod, _maxIntMethod, _absLongMethod, _minLongMethod, _maxLongMethod, _absFloatMethod, _minFloatMethod, _maxFloatMethod, _absDoubleMethod, _minDoubleMethod, _maxDoubleMethod;

		internal static void ExecuteCFlow(MethodDef Method, List<Instruction> InstructionsToMutateFromCFlow) {
			InstructionsToMutate.AddRange(InstructionsToMutateFromCFlow);

			_absIntMethod = Method.Module.Import(typeof(Math).GetMethod("Abs", new[] { typeof(int) }));
			_minIntMethod = Method.Module.Import(typeof(Math).GetMethod("Min", new[] { typeof(int), typeof(int) }));
			_maxIntMethod = Method.Module.Import(typeof(Math).GetMethod("Max", new[] { typeof(int), typeof(int) }));

			_absLongMethod = Method.Module.Import(typeof(Math).GetMethod("Abs", new[] { typeof(long) }));
			_minLongMethod = Method.Module.Import(typeof(Math).GetMethod("Min", new[] { typeof(long), typeof(long) }));
			_maxLongMethod = Method.Module.Import(typeof(Math).GetMethod("Max", new[] { typeof(long), typeof(long) }));

			_absFloatMethod = Method.Module.Import(typeof(Math).GetMethod("Abs", new[] { typeof(float) }));
			_minFloatMethod = Method.Module.Import(typeof(Math).GetMethod("Min", new[] { typeof(float), typeof(float) }));
			_maxFloatMethod = Method.Module.Import(typeof(Math).GetMethod("Max", new[] { typeof(float), typeof(float) }));

			_absDoubleMethod = Method.Module.Import(typeof(Math).GetMethod("Abs", new[] { typeof(double) }));
			_minDoubleMethod = Method.Module.Import(typeof(Math).GetMethod("Min", new[] { typeof(double), typeof(double) }));
			_maxDoubleMethod = Method.Module.Import(typeof(Math).GetMethod("Max", new[] { typeof(double), typeof(double) }));

			if (Method.IsGetter || Method.IsSetter || !Method.HasBody || Method.Body.Instructions.Count() < 5) return;
			Method.Body.SimplifyBranches();
			int numorig = 0, div = 0, num = 0;
			for (int i = 0; i < Method.Body.Instructions.Count; i++) {
				if (Method.Body.Instructions[i].IsLdcI4()) {
					numorig = new Random(Guid.NewGuid().GetHashCode()).Next();
					div = new Random(Guid.NewGuid().GetHashCode()).Next();
					num = numorig ^ div;

					Instruction nop = OpCodes.Nop.ToInstruction();

					Local local = new Local(Method.Module.ImportAsTypeSig(typeof(int)));
					Method.Body.Variables.Add(local);

					int Generated = Generator.RandomInteger(1, 6);

					Method.Body.Instructions.Insert(i + 1, OpCodes.Stloc.ToInstruction(local));
					if (Generated < 2)
						Method.Body.Instructions.Insert(i + 2, Instruction.Create(OpCodes.Ldc_I4, Method.Body.Instructions[i].GetLdcI4Value() - sizeof(float)));
					else if(Generated >= 2 && Generated < 4)
						Method.Body.Instructions.Insert(i + 2, Instruction.Create(OpCodes.Ldc_I4, Method.Body.Instructions[i].GetLdcI4Value() - sizeof(int)));
					else
						Method.Body.Instructions.Insert(i + 2, Instruction.Create(OpCodes.Ldc_I4, Method.Body.Instructions[i].GetLdcI4Value() - 4));

					Generated = Generator.RandomInteger(1, 6);

					Method.Body.Instructions.Insert(i + 3, Instruction.Create(OpCodes.Ldc_I4, num));
					Method.Body.Instructions.Insert(i + 4, Instruction.Create(OpCodes.Ldc_I4, div));
					Method.Body.Instructions.Insert(i + 5, Instruction.Create(OpCodes.Xor));
					Method.Body.Instructions.Insert(i + 6, Instruction.Create(OpCodes.Ldc_I4, numorig));
					Method.Body.Instructions.Insert(i + 7, Instruction.Create(OpCodes.Bne_Un, nop));
					Method.Body.Instructions.Insert(i + 8, Instruction.Create(OpCodes.Ldc_I4, 2));
					Method.Body.Instructions.Insert(i + 9, OpCodes.Stloc.ToInstruction(local));

					if (Generated < 2)
						Method.Body.Instructions.Insert(i + 10, Instruction.Create(OpCodes.Sizeof, Method.Module.Import(typeof(float))));
					else if (Generated >= 2 && Generated < 4)
						Method.Body.Instructions.Insert(i + 10, Instruction.Create(OpCodes.Sizeof, Method.Module.Import(typeof(int))));
					else
						Method.Body.Instructions.Insert(i + 10, Instruction.Create(OpCodes.Ldc_I4, 4));

					Method.Body.Instructions.Insert(i + 11, Instruction.Create(OpCodes.Add));
					Method.Body.Instructions.Insert(i + 12, nop);
					i += 12;
				}
			}
			foreach (Instruction Instruction in Method.Body.Instructions.Where(I => I.OpCode == OpCodes.Ldc_I4).ToArray()) {
				long operand = Instruction.GetLdcI4Value();
				if (InstructionsToMutate.Contains(Instruction)) {
					var i = Method.Body.Instructions.IndexOf(Instruction);

					if (operand >= 0)
						Method.Body.Instructions.Insert(i + 1, OpCodes.Call.ToInstruction(_absIntMethod));

					var neg = Generator.RandomInteger(2, 65);
					if (neg % 2 != 0)
						neg++;

					for (var j = 0; j < neg; j++)
						Method.Body.Instructions.Insert(i + j + 1, OpCodes.Neg.ToInstruction());

					if (operand > 1) {
						Method.Body.Instructions.Insert(i + 1, OpCodes.Ldc_I4.ToInstruction(1));
						Method.Body.Instructions.Insert(i + 2, OpCodes.Call.ToInstruction(_maxIntMethod));
					}

					var randomValue = Generator.RandomInteger();

					for (; ; )
					{
						try {
							_ = checked(operand + randomValue);
							break;
						}
						catch (OverflowException) {
							randomValue = Generator.RandomInteger(randomValue);
						}
					}
				}
			}

			Method.Body.UpdateInstructionOffsets();
		}
	}
}
