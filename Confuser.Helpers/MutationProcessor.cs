using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Helpers {
	public delegate IReadOnlyList<Instruction> PlaceholderProcessor(ModuleDef module, MethodDef method, IReadOnlyList<Instruction> arguments);

	public delegate IReadOnlyList<Instruction> CryptProcessor(ModuleDef module, MethodDef method, Local block, Local key);

	public class MutationProcessor : IMethodInjectProcessor {
		private const string MutationClassName = "Mutation";

		private TypeDef MutationTypeDef { get; }
		private ITraceService TraceService { get; }
		private ModuleDef TargetModule { get; }
		public IReadOnlyDictionary<MutationField, int> KeyFieldValues { get; set; }
		public IReadOnlyDictionary<MutationField, LateMutationFieldUpdate> LateKeyFieldValues { get; set; }
		public PlaceholderProcessor PlaceholderProcessor { get; set; }
		public CryptProcessor CryptProcessor { get; set; }

		public MutationProcessor(IServiceProvider services, ModuleDef targetModule) {
			if (services == null) throw new ArgumentNullException(nameof(services));
			var runtimeService = services.GetRequiredService<IRuntimeService>();
			TraceService = services.GetRequiredService<ITraceService>();
			TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));

			MutationTypeDef = runtimeService.GetRuntimeType(MutationClassName);
		}

		void IMethodInjectProcessor.Process(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.HasBody, $"{nameof(method)}.HasBody");

			var instructions = method.Body.Instructions;
			for (var i = 0; i < instructions.Count; i++) {
				var instr = instructions[i];

				if (instr.OpCode == OpCodes.Ldsfld) {
					if (instr.Operand is IField loadedField && loadedField.DeclaringType == MutationTypeDef) {
						if (!ProcessKeyField(method, instr, loadedField))
							throw new InvalidOperationException("Unexpected load field operation to Mutation class!");
					}
				}
				else if (instr.OpCode == OpCodes.Call) {
					if (instr.Operand is IMethod calledMethod && calledMethod.DeclaringType == MutationTypeDef) {
						if (!ReplacePlaceholder(method, instr, calledMethod, ref i) && !ReplaceCrypt(method, instr, calledMethod, ref i))
							throw new InvalidOperationException("Unexpected call operation to Mutation class!");
					}
				}
			}
		}

		private bool ProcessKeyField(MethodDef method, Instruction instr, IField field) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(instr != null, $"{nameof(instr)} != null");
			Debug.Assert(field != null, $"{nameof(field)} != null");

			if (field.Name?.Length >= 5 && field.Name.StartsWith("KeyI")) {
				var number = field.Name.String.AsSpan().Slice(start: 4, length: (field.Name.Length == 5 ? 1 : 2));
				if (int.TryParse(number.ToString(), out var value)) {
					MutationField mutationField;
					switch (value) {
						case 0: mutationField = MutationField.KeyI0; break;
						case 1: mutationField = MutationField.KeyI1; break;
						case 2: mutationField = MutationField.KeyI2; break;
						case 3: mutationField = MutationField.KeyI3; break;
						case 4: mutationField = MutationField.KeyI4; break;
						case 5: mutationField = MutationField.KeyI5; break;
						case 6: mutationField = MutationField.KeyI6; break;
						case 7: mutationField = MutationField.KeyI7; break;
						case 8: mutationField = MutationField.KeyI8; break;
						case 9: mutationField = MutationField.KeyI9; break;
						case 10: mutationField = MutationField.KeyI10; break;
						case 11: mutationField = MutationField.KeyI11; break;
						case 12: mutationField = MutationField.KeyI12; break;
						case 13: mutationField = MutationField.KeyI13; break;
						case 14: mutationField = MutationField.KeyI14; break;
						case 15: mutationField = MutationField.KeyI15; break;
						default: return false;
					}

					if (KeyFieldValues != null && KeyFieldValues.TryGetValue(mutationField, out var keyValue)) {
						instr.OpCode = OpCodes.Ldc_I4;
						instr.Operand = keyValue;
						return true;
					}
					else if (LateKeyFieldValues != null && LateKeyFieldValues.TryGetValue(mutationField, out var lateUpdate)) {
						lateUpdate.AddUpdateInstruction(method, instr);
						// Setting a dummy value, so the reference to the Mutation class is not injected.
						instr.OpCode = OpCodes.Ldc_I4_0;
						instr.Operand = null;
						return true;
					}
					else {
						throw new InvalidOperationException($"Code contains request to mutation key {field.Name}, but the value for this field is not set.");
					}
				}
			}

			return false;
		}

		private bool ReplacePlaceholder(MethodDef method, Instruction instr, IMethod calledMethod, ref int index) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(instr != null, $"{nameof(instr)} != null");
			Debug.Assert(calledMethod != null, $"{nameof(calledMethod)} != null");

			if (calledMethod.Name == "Placeholder") {
				if (PlaceholderProcessor == null) throw new InvalidOperationException("Found mutation placeholder, but there is no processor defined.");
				var trace = TraceService.Trace(method);
				int[] argIndexes = trace.TraceArguments(instr);
				if (argIndexes == null) throw new InvalidOperationException("Failed to trace placeholder argument.");

				int argIndex = argIndexes[0];
				IReadOnlyList<Instruction> arg = method.Body.Instructions.Skip(argIndex).Take(index - argIndex).ToImmutableArray();

				for (int j = 0; j < arg.Count; j++)
					method.Body.Instructions.RemoveAt(argIndex);
				method.Body.Instructions.RemoveAt(argIndex);

				index -= arg.Count;

				arg = PlaceholderProcessor(TargetModule, method, arg);
				for (int j = arg.Count - 1; j >= 0; j--)
					method.Body.Instructions.Insert(argIndex, arg[j]);

				index += arg.Count;

				return true;
			}

			return false;
		}

		private bool ReplaceCrypt(MethodDef method, Instruction instr, IMethod calledMethod, ref int index) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(instr != null, $"{nameof(instr)} != null");
			Debug.Assert(calledMethod != null, $"{nameof(calledMethod)} != null");

			if (calledMethod.Name == "Crypt") {
				if (CryptProcessor == null) throw new InvalidOperationException("Found mutation crypt, but not processor defined.");

				var instrIndex = method.Body.Instructions.IndexOf(instr);
				var ldBlock = method.Body.Instructions[instrIndex - 2];
				var ldKey = method.Body.Instructions[instrIndex - 1];
				Debug.Assert(ldBlock.OpCode == OpCodes.Ldloc && ldKey.OpCode == OpCodes.Ldloc);

				method.Body.Instructions.RemoveAt(instrIndex);
				method.Body.Instructions.RemoveAt(instrIndex - 1);
				method.Body.Instructions.RemoveAt(instrIndex - 2);

				var cryptInstr = CryptProcessor(TargetModule, method, (Local)ldBlock.Operand, (Local)ldKey.Operand);
				for (var i = 0; i< cryptInstr.Count; i++) {
					method.Body.Instructions.Insert(instrIndex - 2 + i, cryptInstr[i]);
				}

				index += cryptInstr.Count - 3;

				return true;
			}
			return false;
		}
	}
}
