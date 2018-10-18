using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexConstructorCompiler : RegexMethodCompiler {
		private readonly TypeDef _regexTypeDef;
		private readonly MethodDef _regexCtorDef;
		private readonly MethodDef _initializeReferencesMethodDef;

		private readonly FieldDef _patternFieldDef;
		private readonly FieldDef _roptionsFieldDef;
		private readonly FieldDef _regexFactoryFieldDef;
		private readonly FieldDef _capsFieldDef;
		private readonly FieldDef _capnamesFieldDef;
		private readonly FieldDef _capslistFieldDef;
		private readonly FieldDef _capsizeFieldDef;
		private readonly FieldDef _internalMatchTimeoutFieldDef;
		private readonly FieldDef _defaultMatchTimeoutFieldDef;
		private readonly MethodDef _validateMatchTimeoutMethodDef;

		private readonly ITypeDefOrRef _stringTypeRef;
		private readonly ITypeDefOrRef _int32TypeRef;
		private readonly TypeDef _timespanTypeDef;
		private readonly TypeSig _timespanTypeSig;


		private readonly MethodDef _timespanFromTicksMethodDef;

		internal RegexConstructorCompiler(ModuleDef module, MethodDef method) : base(module, method) {
			Debug.Assert(module != null, $"{nameof(module)} != null");
			Debug.Assert(method != null, $"{nameof(method)} != null");

			var typeRefFinder = new TypeRefFinder(module);
			_regexTypeDef = typeRefFinder.FindType(CompileRegexProtection._RegexTypeFullName).ResolveTypeDefThrow();
			_regexCtorDef = _regexTypeDef.FindDefaultConstructor();
			_initializeReferencesMethodDef = _regexTypeDef.FindMethod("InitializeReferences", MethodSig.CreateInstance(module.CorLibTypes.Void));

			const string ns = CompileRegexProtection._RegexNamespace;
			var regexOptionsTypeSig = typeRefFinder.FindType(ns + ".RegexOptions").ToTypeSig();
			var regexRunnerFactoryTypeSig = typeRefFinder.FindType(ns + ".RegexRunnerFactory").ToTypeSig();
			_timespanTypeDef = typeRefFinder.FindType("System.TimeSpan").ResolveTypeDefThrow();
			_timespanTypeSig = _timespanTypeDef.ToTypeSig();

			_patternFieldDef = _regexTypeDef.FindField("pattern", new FieldSig(module.CorLibTypes.String), SigComparerOptions.PrivateScopeFieldIsComparable);
			_roptionsFieldDef = _regexTypeDef.FindField("roptions", new FieldSig(regexOptionsTypeSig), SigComparerOptions.PrivateScopeFieldIsComparable);
			_regexFactoryFieldDef = _regexTypeDef.FindField("factory", new FieldSig(regexRunnerFactoryTypeSig), SigComparerOptions.PrivateScopeFieldIsComparable);
			_capsFieldDef = _regexTypeDef.FindField("caps");
			_capnamesFieldDef = _regexTypeDef.FindField("capnames");
			_capslistFieldDef = _regexTypeDef.FindField("capslist", new FieldSig(new SZArraySig(module.CorLibTypes.String)), SigComparerOptions.PrivateScopeFieldIsComparable);
			_capsizeFieldDef = _regexTypeDef.FindField("capsize", new FieldSig(module.CorLibTypes.Int32), SigComparerOptions.PrivateScopeFieldIsComparable);
			_internalMatchTimeoutFieldDef = _regexTypeDef.FindField("internalMatchTimeout", new FieldSig(_timespanTypeSig), SigComparerOptions.PrivateScopeFieldIsComparable);
			_validateMatchTimeoutMethodDef = _regexTypeDef.FindMethod("ValidateMatchTimeout", MethodSig.CreateStatic(module.CorLibTypes.Void, _timespanTypeSig), SigComparerOptions.PrivateScopeFieldIsComparable);


			_defaultMatchTimeoutFieldDef = _regexTypeDef.FindField("DefaultMatchTimeout", new FieldSig(_timespanTypeSig), SigComparerOptions.PrivateScopeFieldIsComparable);

			_stringTypeRef = module.CorLibTypes.String.ToTypeDefOrRef();
			_int32TypeRef = module.CorLibTypes.Int32.ToTypeDefOrRef();
			_timespanFromTicksMethodDef = _timespanTypeDef.FindMethod("FromTicks", MethodSig.CreateStatic(_timespanTypeSig, module.CorLibTypes.Int64));
		}

		internal void GenerateDefaultConstructor(TypeDef factory, RegexCompileDef expression, RegexCode code, RegexTree tree) {
			Debug.Assert(factory != null, $"{nameof(factory)} != null");

			Call(_regexCtorDef);

			// Set Pattern
			Stfld(_patternFieldDef, () => Ldstr(expression.Pattern));

			// Set Options
			Stfld(_roptionsFieldDef, () => Ldc((int)expression.Options));

			if (_internalMatchTimeoutFieldDef == null) {
				// This seems to be a .NET version prior to .NET 4.5
				// This means that there is no timeout support at all.
				Debug.Assert(expression.StaticTimeout, "Only static timeout supported for old .NET.");
				Debug.Assert(!expression.Timeout.HasValue, "Timeout is not supported for old .NET.");
			}
			else if (expression.StaticTimeout && expression.Timeout.HasValue) {
				var ticks = expression.Timeout.Value.Ticks;

				// Set the timeout to the known static value.
				Stfld(_internalMatchTimeoutFieldDef, () => {
					Ldc(ticks);
					Call(_timespanFromTicksMethodDef);
				});
			}
			else {
				// Set the timeout to the default value
				if (_defaultMatchTimeoutFieldDef.IsFamily ||
					_defaultMatchTimeoutFieldDef.IsFamilyOrAssembly ||
					_defaultMatchTimeoutFieldDef.IsPublic)
					Stfld(_internalMatchTimeoutFieldDef, () => Ldfld(_defaultMatchTimeoutFieldDef));
			}


			// set factory
			Stfld(_regexFactoryFieldDef, () => Newobj(factory.FindDefaultConstructor()));

			// set caps
			if (code._caps != null)
				GenerateCreateTable(_capsFieldDef, code._caps);

			// set capnames
			if (tree._capnames != null)
				GenerateCreateTable(_capnamesFieldDef, tree._capnames);


			// set capslist
			if (tree._capslist != null) {
				Stfld(_capslistFieldDef, () => Newarr(_stringTypeRef, tree._capslist.Length));

				for (int i = 0; i < tree._capslist.Length; i++) {
					Ldfld(_capslistFieldDef);

					Ldc(i);
					Ldstr(tree._capslist[i]);
					Add(Instruction.Create(OpCodes.Stelem_Ref));
				}
			}

			// set capsize
			Stfld(_capsizeFieldDef, () => Ldc(code._capsize));

			// set runnerref and replref by calling InitializeReferences()
			Ldthis();
			Callvirt(_initializeReferencesMethodDef);

			Ret();
		}

		internal void GenerateTimeoutConstructor(MethodDef defaultConstructor) {
			Debug.Assert(defaultConstructor != null, $"{nameof(defaultConstructor)} != null");

			Call(defaultConstructor);
			Ldarg(Method.Parameters[1]);
			Call(_validateMatchTimeoutMethodDef);
			Stfld(_internalMatchTimeoutFieldDef, () => Ldarg(Method.Parameters[1]));
			Ret();
		}


		internal void GenerateCreateTable(FieldDef field, IDictionary ht) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(ht != null, $"{nameof(ht)} != null");

			var fieldTypeDef = field.FieldType.TryGetTypeRef().ResolveThrow();

			var addMethod = fieldTypeDef.FindMethods("Add").Where(m => m.MethodSig.Params.Count == 2).FirstOrDefault();
			if (addMethod == null) throw new InvalidOperationException("There is no add method for this list?!");

			Stfld(field, () => Newobj(fieldTypeDef.FindDefaultConstructor()));

			var en = ht.GetEnumerator();
			while (en.MoveNext()) {
				Ldfld(field);

				if (en.Key is int intKey) {
					Ldc(intKey);

					if (!addMethod.Parameters[0].Type.GetIsValueType()) {
						Box(_int32TypeRef);
					}
				}
				else
					Ldstr((string)en.Key);

				Ldc((int)en.Value);
				if (!addMethod.Parameters[1].Type.GetIsValueType()) {
					Box(_int32TypeRef);
				}

				Callvirt(addMethod);
			}
		}
	}
}
