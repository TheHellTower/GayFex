using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal class RegexRunnerMethodCompiler : RegexMethodCompiler {
		protected readonly RegexRunnerDef _regexRunnerDef;
		private readonly IDictionary<FieldDef, Local> _cachedFields;

		private readonly TypeDef _cultureInfoTypeDef;
		private readonly MethodDef _cultureGetInvariantCultureMethodDef;

		private readonly MethodDef _stringGetCharsMethodDef;
		private readonly MethodDef _charToLowerMethodDef;
		private readonly MethodDef _charToLowerCultureInfoMethodDef;
		private readonly MethodDef _charToLowerInvariantMethodDef;

		internal bool CacheRegexRunnerFieldsInLocalVariables { get; set; } = true;

		internal RegexRunnerMethodCompiler(ModuleDef module, MethodDef method, RegexRunnerDef regexRunnerDef) : base(
			module, method) {
			_regexRunnerDef = regexRunnerDef ?? throw new ArgumentNullException(nameof(regexRunnerDef));
			_cachedFields = new Dictionary<FieldDef, Local>();

			var typeRefFinder = new TypeRefFinder(module);

			_cultureInfoTypeDef = typeRefFinder.FindType("System.Globalization.CultureInfo").ResolveTypeDefThrow();
			_cultureGetInvariantCultureMethodDef = _cultureInfoTypeDef.FindMethod("get_InvariantCulture",
				MethodSig.CreateStatic(_cultureInfoTypeDef.ToTypeSig()));

			var charTypeSig = module.CorLibTypes.Char;
			var stringTypeDef = module.CorLibTypes.String.ToTypeDefOrRef().ResolveTypeDefThrow();
			_stringGetCharsMethodDef = stringTypeDef.FindMethod("get_Chars",
				MethodSig.CreateInstance(charTypeSig, module.CorLibTypes.Int32));

			var charTypeDef = charTypeSig.ToTypeDefOrRef().ResolveTypeDefThrow();
			_charToLowerMethodDef = charTypeDef.FindMethod("ToLower", MethodSig.CreateStatic(charTypeSig, charTypeSig));
			_charToLowerCultureInfoMethodDef = charTypeDef.FindMethod("ToLower",
				MethodSig.CreateStatic(charTypeSig, charTypeSig, _cultureInfoTypeDef.ToTypeSig()));
			_charToLowerInvariantMethodDef =
				charTypeDef.FindMethod("ToLowerInvariant", MethodSig.CreateStatic(charTypeSig, charTypeSig));
		}

		internal override void FinishMethod() {
			SeekStart();

			foreach (var field in _cachedFields.Keys)
				UpdateFieldCache(field);

			SeekEnd();
			base.FinishMethod();
		}

		internal void UpdateCachedField(FieldDef field) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(field.DeclaringType == _regexRunnerDef.RegexRunnerTypeDef,
				@"The fields used are expected to be part of the RegexRunner class.");

			if (_cachedFields.TryGetValue(field, out var local)) {
				MvLocalToField(local, field);
			}
		}

		internal void UpdateFieldCache(FieldDef field) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(field.DeclaringType == _regexRunnerDef.RegexRunnerTypeDef,
				@"The fields used are expected to be part of the RegexRunner class.");

			if (_cachedFields.TryGetValue(field, out var local)) {
				MvFieldToLocal(field, local);
			}
		}

		internal void LdRunnerField(FieldDef field) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(field.DeclaringType == _regexRunnerDef.RegexRunnerTypeDef,
				@"The fields used are expected to be part of the RegexRunner class.");

			if (CacheRegexRunnerFieldsInLocalVariables) {
				if (!_cachedFields.TryGetValue(field, out var local)) {
					_cachedFields[field] = local = RequireLocal(field.FieldType, false);
				}

				Ldloc(local);
			}
			else {
				Ldfld(field);
			}
		}

		internal void StRunnerField(FieldDef field) => StRunnerField(field, delegate { });

		internal void StRunnerField(FieldDef field, Action loadValue) {
			Debug.Assert(field != null, $"{nameof(field)} != null");
			Debug.Assert(field.DeclaringType == _regexRunnerDef.RegexRunnerTypeDef,
				@"The fields used are expected to be part of the RegexRunner class.");

			if (CacheRegexRunnerFieldsInLocalVariables) {
				if (!_cachedFields.TryGetValue(field, out var local)) {
					_cachedFields[field] = local = RequireLocal(field.FieldType, false);
				}

				loadValue();
				Stloc(local);
			}
			else {
				Stfld(field, loadValue);
			}
		}

		/*
		 * Loads the char to the left of the current position
		 */
		internal void Leftchar() => Leftchar(false);

		/*
		 * Loads the char to the left of the current position and advances (leftward)
		 */
		internal void Leftcharnext() => Leftchar(true);

		private void Leftchar(bool advance) {
			LdRunnerField(_regexRunnerDef.runtextFieldDef);
			LdRunnerField(_regexRunnerDef.runtextposFieldDef);
			Ldc(1);
			Sub();
			if (advance) {
				Dup();
				StRunnerField(_regexRunnerDef.runtextposFieldDef);
			}

			CallStringGetChars();
		}

		/*
		 * Loads the char to the right of the current position
		 */
		internal void Rightchar() => Rightchar(false);

		/*
		 * Loads the char to the right of the current position and advances the current position
		 */
		internal void Rightcharnext() => Rightchar(true);

		private void Rightchar(bool advance) {
			LdRunnerField(_regexRunnerDef.runtextFieldDef);
			LdRunnerField(_regexRunnerDef.runtextposFieldDef);
			if (advance) {
				Dup();
				Ldc(1);
				Add();
				StRunnerField(_regexRunnerDef.runtextposFieldDef);
			}

			CallStringGetChars();
		}

		protected void GetChar(int offset) {
			LdRunnerField(_regexRunnerDef.runtextFieldDef);
			LdRunnerField(_regexRunnerDef.runtextposFieldDef);
			if (offset > 0) {
				Ldc(offset);
				Add();
			}
			else if (offset < 0) {
				Ldc(-offset);
				Sub();
			}

			CallStringGetChars();
		}

		protected void CallStringGetChars() => Callvirt(_stringGetCharsMethodDef);

		internal void CallIsMatched(int cap) {
			Ldthis();
			Ldc(cap);
			Callvirt(_regexRunnerDef.IsMatchedMethodDef);
		}

		internal void CallMatchIndex(int cap) {
			Ldthis();
			Ldc(cap);
			Callvirt(_regexRunnerDef.MatchIndexMethodDef);
		}

		internal void CallMatchLength(int cap) {
			Ldthis();
			Ldc(cap);
			Callvirt(_regexRunnerDef.MatchLengthMethodDef);
		}

		internal void CallTransferCapture(int capnum, int uncapnum, Local start, FieldDef end) {
			Ldthis();
			Ldc(capnum);
			Ldc(uncapnum);
			Ldloc(start);
			LdRunnerField(end);
			Callvirt(_regexRunnerDef.TransferCaptureMethodDef);
		}

		internal void CallCapture(int capnum, Local start, FieldDef end) {
			Ldthis();
			Ldc(capnum);
			Ldloc(start);
			LdRunnerField(end);
			Callvirt(_regexRunnerDef.CaptureMethodDef);
		}

		internal void CallUncapture() {
			Ldthis();
			Callvirt(_regexRunnerDef.UncaptureMethodDef);
		}

		internal void CallCrawlpos() {
			Ldthis();
			Callvirt(_regexRunnerDef.CrawlposMethodDef);
		}

		internal void CallIsBoundary(FieldDef index, FieldDef startPos, FieldDef endPos) {
			Ldthis();
			LdRunnerField(index);
			LdRunnerField(startPos);
			LdRunnerField(endPos);
			Callvirt(_regexRunnerDef.IsBoundaryMethodDef);
		}

		internal void CallIsECMABoundary(FieldDef index, FieldDef startPos, FieldDef endPos) {
			Ldthis();
			LdRunnerField(index);
			LdRunnerField(startPos);
			LdRunnerField(endPos);
			Callvirt(_regexRunnerDef.IsECMABoundaryMethodDef);
		}

		internal void CallCharInClass(string charClass) {
			Ldstr(charClass);
			Call(_regexRunnerDef.CharInClassMethodDef);
		}

		internal void CallEnsureStorage() {
			Ldthis();
			Callvirt(_regexRunnerDef.EnsureStorageMethodDef);
		}

		internal void CallCheckTimeout() {
			Ldthis();
			Callvirt(_regexRunnerDef.CheckTimeoutMethodDef);
		}

		internal void CallToLower(RegexOptions options) {
			if ((options & RegexOptions.CultureInvariant) != 0) {
				// Using the invariant culture.
				if (_charToLowerInvariantMethodDef != null) {
					// In case the char.ToLowerInvariant method is available, we'll use it.
					Call(_charToLowerInvariantMethodDef);
				}
				else {
					// The ToLowerInvariant method is not available, so we use the normal ToLower method with the
					// InvariantCulture.
					Call(_cultureGetInvariantCultureMethodDef);
					Call(_charToLowerCultureInfoMethodDef);
				}
			}
			else {
				// The current culture should be used. To char.ToLower function without explicit culture info does
				// just that.
				Call(_charToLowerMethodDef);
			}
		}

		/*
		 * A macro for _ilg.Emit(OpCodes.Add); a true flag can turn it into a Sub
		 */
		internal void Add(bool negate) {
			if (negate) Sub();
			else Add();
		}

		/*
		 * A macro for _ilg.Emit(OpCodes.Sub); a true flag can turn it into a Ad
		 */
		internal void Sub(bool negate) => Add(!negate);
	}
}
