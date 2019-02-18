using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal class RegexMethodCompiler {
		protected MethodDef Method { get; }

		private Importer _importer;

		private readonly IList<Instruction> _instructions;
		private int _insertIndex;

		internal RegexMethodCompiler(ModuleDef module, MethodDef method) {
			if (module == null) throw new ArgumentNullException(nameof(module));
			Method = method ?? throw new ArgumentNullException(nameof(method));

			_importer = new Importer(module, ImporterOptions.TryToUseDefs);

			var methodBody = method.Body ?? (Method.Body = new CilBody());

			if (methodBody.HasInstructions)
				throw new ArgumentException("The compiler expects a target method with a empty body.", nameof(method));
			_instructions = methodBody.Instructions;
			_insertIndex = 0;
		}

		protected void Add(Instruction instruction) {
			Debug.Assert(instruction != null, $"{nameof(instruction)} != null");

			_instructions.Insert(_insertIndex, instruction);
			_insertIndex++;
		}

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Ldthis")]
		internal void Ldthis() {
			var thisParameter = Method.Parameters[0];
			Debug.Assert(thisParameter.IsHiddenThisParameter,
				"Tried to load \"this\", but method does not contain the parameter. Static method?");

			Add(Instruction.Create(OpCodes.Ldarg, thisParameter));
		}

		internal void Ret() => Add(Instruction.Create(OpCodes.Ret));

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Ldarg")]
		protected void Ldarg(Parameter parameter) {
			Debug.Assert(parameter != null, $"{nameof(parameter)} != null");

			Add(Instruction.Create(OpCodes.Ldarg, parameter));
		}

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Ldfld")]
		protected void Ldfld(FieldDef field) {
			Debug.Assert(field != null, $"{nameof(field)} != null");

			if (!field.IsStatic) Ldthis();
			Add(Instruction.Create(field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, _importer.Import(field)));
		}

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Stfld")]
		protected void Stfld(FieldDef field, Action loadValue) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(loadValue != null, $"{nameof(loadValue)} != null");

			if (!field.IsStatic) Ldthis();

			loadValue();
			Add(Instruction.Create(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, _importer.Import(field)));
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

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Ldstr")]
		internal void Ldstr(string value) => Add(Instruction.Create(OpCodes.Ldstr, value));

		// https://youtu.be/fTu5SkLVf-o
		internal void Box(ITypeDefOrRef typeRef) {
			Debug.Assert(typeRef != null, $"{nameof(typeRef)} != null");

			Add(Instruction.Create(OpCodes.Box, _importer.Import(typeRef)));
		}

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Callvirt")]
		internal void Callvirt(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(!method.IsStatic, "Virtual call to static method?");

			Add(Instruction.Create(OpCodes.Callvirt, _importer.Import(method)));
		}

		internal void Call(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			if (method.IsStatic)
				Add(Instruction.Create(OpCodes.Call, _importer.Import(method)));
			else if (method.IsConstructor) {
				Ldthis();
				Add(Instruction.Create(OpCodes.Call, _importer.Import(method)));
			}
			else
				Debug.Assert(method.IsStatic, "Call to non-static method?");
		}

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Newobj")]
		internal void Newobj(MethodDef method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.IsConstructor, "New object and no constructor?");

			Add(Instruction.Create(OpCodes.Newobj, _importer.Import(method)));
		}

		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches OpCodes.Newarr")]
		internal void Newarr(ITypeDefOrRef arrayType, int size) {
			Debug.Assert(arrayType != null, $"{nameof(arrayType)} != null");
			Debug.Assert(size >= 0, $"{nameof(size)} >= 0");

			Ldc(size);
			Add(Instruction.Create(OpCodes.Newarr, arrayType));
		}
	}
}
