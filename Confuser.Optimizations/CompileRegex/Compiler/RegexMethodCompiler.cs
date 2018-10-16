using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal class RegexMethodCompiler {
		private readonly ModuleDef _module;
		protected MethodDef Method { get; }

		private readonly Importer _importer;

		private readonly CilBody _methodBody;
		private readonly IList<Instruction> _instructions;
		private int _insertIndex;

		private readonly IDictionary<RegexMethodCompilerLabel, Instruction> _labels;
		private readonly ISet<RegexMethodCompilerLabel> _unknownLabels;
		private readonly IList<RegexMethodCompilerLabel> _labelsNextInstruction;

		private readonly IList<Local> _requiredLocals;
		private readonly IDictionary<TypeSig, IList<Local>> _unusedLocals;

		internal RegexMethodCompiler(ModuleDef module, MethodDef method) {
			_module = module ?? throw new ArgumentNullException(nameof(module));
			Method = method ?? throw new ArgumentNullException(nameof(method));

			_importer = new Importer(_module, ImporterOptions.TryToUseDefs);

			_methodBody = method.Body;
			if (_methodBody == null)
				_methodBody = Method.Body = new CilBody();

			if (_methodBody.HasInstructions) throw new ArgumentException("The compiler expects a target method with a empty body.", nameof(method));
			_instructions = _methodBody.Instructions;
			_insertIndex = 0;

			_labels = new Dictionary<RegexMethodCompilerLabel, Instruction>();
			_unknownLabels = new HashSet<RegexMethodCompilerLabel>();
			_labelsNextInstruction = new List<RegexMethodCompilerLabel>(1);

			_requiredLocals = new List<Local>();
			_unusedLocals = new Dictionary<TypeSig, IList<Local>>();
		}

		internal Local RequireLocalInt32() => RequireLocal(_module.CorLibTypes.Int32);

		internal Local RequireLocal(TypeSig sig) => RequireLocal(sig, true);

		protected Local RequireLocal(TypeSig sig, bool allowReused) {
			Debug.Assert(sig != null, $"{nameof(sig)} != null");

			if (allowReused && _unusedLocals.TryGetValue(sig, out var unusedLocals)) {
				if (unusedLocals != null && unusedLocals.Count > 0) {
					var reusedLocal = unusedLocals[unusedLocals.Count - 1];
					unusedLocals.RemoveAt(unusedLocals.Count - 1);
					return reusedLocal;
				}
			}
			var local = new Local(sig);
			_methodBody.Variables.Add(local);
			return local;
		}

		internal void FreeLocal(Local local) {
			Debug.Assert(local != null, $"{nameof(local)} != null");

			if (!_unusedLocals.TryGetValue(local.Type, out var unusedLocals)) {
				unusedLocals = _unusedLocals[local.Type] = new List<Local>();
			}
			unusedLocals.Add(local);
		}

		internal void MarkLabel(RegexMethodCompilerLabel label) {
			Debug.Assert(label != null, $"{nameof(label)} != null");

			_labelsNextInstruction.Add(label);
		}

		protected void Add(Instruction instruction) {
			Debug.Assert(instruction != null, $"{nameof(instruction)} != null");

			if (_labelsNextInstruction.Count > 0)
				ResolveLabels(instruction);

			_instructions.Insert(_insertIndex, instruction);
			_insertIndex++;
		}

		protected void SeekStart() => _insertIndex = 0;
		protected void SeekEnd() => _insertIndex = _instructions.Count;
		protected void Seek(int index) => _insertIndex = index;
		protected int NextIndex() => _insertIndex;

		private void ResolveLabels(Instruction instruction) {
			Debug.Assert(instruction != null, $"{nameof(instruction)} != null");
			Debug.Assert(_labelsNextInstruction.Count > 0, $"{nameof(_labelsNextInstruction)}.Count > 0");

			foreach (var label in _labelsNextInstruction) {
				if (_labels.TryGetValue(label, out var instr)) {
					if (!_unknownLabels.Remove(label))
						throw new ArgumentException("Label was marked more than once!", nameof(label));

					foreach (var testInstr in _instructions) {
						if (instr.Equals(testInstr.Operand))
							testInstr.Operand = instruction;
					}
				}
				_labels[label] = instruction;
			}
			_labelsNextInstruction.Clear();
		}

		internal void Ldthis() {
			var thisParameter = Method.Parameters[0];
			Debug.Assert(thisParameter.IsHiddenThisParameter, "Tried to load \"this\", but method does not contain the parameter. Static method?");

			Add(Instruction.Create(OpCodes.Ldarg, thisParameter));
		}

		internal void Ldloc(Local local) {
			Debug.Assert(local != null, $"{nameof(local)} != null");
			Add(Instruction.Create(OpCodes.Ldloc, local));
		}

		internal void Stloc(Local local) {
			Debug.Assert(local != null, $"{nameof(local)} != null");
			Add(Instruction.Create(OpCodes.Stloc, local));
		}

		internal void Ret() => Add(Instruction.Create(OpCodes.Ret));
		internal void Add() => Add(Instruction.Create(OpCodes.Add));
		internal void Sub() => Add(Instruction.Create(OpCodes.Sub));
		internal void Dup() => Add(Instruction.Create(OpCodes.Dup));
		internal void Pop() => Add(Instruction.Create(OpCodes.Pop));

		protected void Ldarg(Parameter parameter) {
			Debug.Assert(parameter != null, $"{nameof(parameter)} != null");

			Add(Instruction.Create(OpCodes.Ldarg, parameter));
		}

		protected void Ldfld(FieldDef field) {
			Debug.Assert(field != null, $"{nameof(field)} != null");

			if (!field.IsStatic) Ldthis();
			Add(Instruction.Create(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, _importer.Import(field)));
		}

		protected void Stfld(FieldDef field, Action loadValue) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(loadValue != null, $"{nameof(loadValue)} != null");

			if (!field.IsStatic) Ldthis();

			loadValue();
			Add(Instruction.Create(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, _importer.Import(field)));
		}

		internal void Br(RegexMethodCompilerLabel label) => Branch(OpCodes.Br, label);

		internal void Beq(RegexMethodCompilerLabel label) => Branch(OpCodes.Beq, label);
		internal void Bne(RegexMethodCompilerLabel label) => Branch(OpCodes.Bne_Un, label);

		internal void Brtrue(RegexMethodCompilerLabel label) => Branch(OpCodes.Brtrue, label);

		internal void Brfalse(RegexMethodCompilerLabel label) => Branch(OpCodes.Brfalse, label);

		internal void Blt(RegexMethodCompilerLabel label) => Branch(OpCodes.Blt, label);

		internal void Ble(RegexMethodCompilerLabel label) => Branch(OpCodes.Ble, label);

		internal void Bgt(RegexMethodCompilerLabel label) => Branch(OpCodes.Bgt, label);

		internal void Bge(RegexMethodCompilerLabel label) => Branch(OpCodes.Bge, label);

		internal void Bgtun(RegexMethodCompilerLabel label) => Branch(OpCodes.Bgt_Un, label);

		private void Branch(OpCode opCode, RegexMethodCompilerLabel label) {
			Debug.Assert(opCode != null, $"{nameof(opCode)} != null");
			Debug.Assert(label != null, $"{nameof(label)} != null");
			Debug.Assert(opCode.FlowControl == FlowControl.Branch || opCode.FlowControl == FlowControl.Cond_Branch,
				"Opcode is expected to be a branch");

			Add(Instruction.Create(opCode, GetLabelInstruction(label)));
		}

		protected Instruction GetLabelInstruction(RegexMethodCompilerLabel label) {
			Debug.Assert(label != null, $"{nameof(label)} != null");

			if (!_labels.TryGetValue(label, out var instr)) {
				_labels[label] = instr = new Instruction();
				_unknownLabels.Add(label);
			}
			return instr;
		}

		internal void Ldc(int value) => Add(Instruction.Create(OpCodes.Ldc_I4, value));

		internal void Ldc(long value) {
			if (value <= int.MaxValue && value >= int.MinValue) {
				Ldc((int)value);
				Add(Instruction.Create(OpCodes.Conv_I8));
			}
			else
				Add(Instruction.Create(OpCodes.Ldc_I8, value));
		}

		internal void Ldstr(string value) => Add(Instruction.Create(OpCodes.Ldstr, value));

		internal void Ldlen() => Add(Instruction.Create(OpCodes.Ldlen));

		// https://youtu.be/fTu5SkLVf-o
		internal void Box(ITypeDefOrRef typeRef) {
			Debug.Assert(typeRef != null, $"{nameof(typeRef)} != null");

			Add(Instruction.Create(OpCodes.Box, typeRef));
		}

		internal void Callvirt(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(!method.IsStatic, "Virtual call to static method?");

			Add(Instruction.Create(OpCodes.Callvirt, _importer.Import(method)));

		}

		internal void Call(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			if (method.IsStatic) {
				Add(Instruction.Create(OpCodes.Call, _importer.Import(method)));
			}
			else if (method.IsConstructor) {
				Ldthis();
				Add(Instruction.Create(OpCodes.Call, _importer.Import(method)));
			}
			else
				Debug.Assert(method.IsStatic, "Call to non-static method?");
		}

		internal void Newobj(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.IsConstructor, "New object and no constructor?");

			Add(Instruction.Create(OpCodes.Newobj, _importer.Import(method)));
		}

		internal void Newarr(ITypeDefOrRef arrayType, int size) {
			Debug.Assert(arrayType != null, $"{nameof(arrayType)} != null");
			Debug.Assert(size >= 0, $"{nameof(size)} >= 0");

			Ldc(size);
			Add(Instruction.Create(OpCodes.Newarr, arrayType));
		}

		internal void MvLocalToField(Local local, FieldDef field) {
			Debug.Assert(local != null, $"{nameof(local)} != null");
			Debug.Assert(field != null, $"{nameof(field)} != null");

			Stfld(field, () => Ldloc(local));
		}

		internal void MvFieldToLocal(FieldDef field, Local local) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(local != null, $"{nameof(local)} != null");

			Ldfld(field);
			Stloc(local);
		}

		internal virtual void FinishMethod() {
			if (_unknownLabels.Any())
				throw new InvalidOperationException("There are unresolved labels!");
		}

		protected static RegexMethodCompilerLabel CreateLabel() => new RegexMethodCompilerLabel();
	}
}
