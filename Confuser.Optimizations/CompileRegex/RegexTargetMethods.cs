using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex {
	internal sealed class RegexTargetMethods : IRegexTargetMethods {
		private IImmutableList<RegexTargetMethod> Methods { get; }

		internal RegexTargetMethods(TypeDef regexType) {
			if (regexType == null) throw new ArgumentNullException(nameof(regexType));
			Debug.Assert(regexType.FullName == CompileRegexProtection._RegexTypeFullName,
				$"Unexpected name for RegEx type: {regexType}");

			const string regexNs = CompileRegexProtection._RegexNamespace;

			var voidType = regexType.Module.CorLibTypes.Void;
			var stringType = regexType.Module.CorLibTypes.String;
			var stringArrType = new SZArraySig(stringType);
			var boolType = regexType.Module.CorLibTypes.Boolean;
			var regexOptionsType = regexType.Module.FindNormalThrow(regexNs + ".RegexOptions").ToTypeSig();
			var regexMatchType = regexType.Module.FindNormalThrow(regexNs + ".Match").ToTypeSig();
			var regexMatchColType = regexType.Module.FindNormalThrow(regexNs + ".MatchCollection").ToTypeSig();
			var regexMatchEvalType = regexType.Module.FindNormalThrow(regexNs + ".MatchEvaluator").ToTypeSig();
			var timeSpanType = regexType.Module.GetTypeRefs().Where(t => t.FullName == "System.TimeSpan").First().ToTypeSig();

			Methods = ImmutableArray.Create(
				ScanMethod(regexType.FindMethod(".ctor", MethodSig.CreateInstance(voidType, stringType))),
				ScanMethod(regexType.FindMethod(".ctor", MethodSig.CreateInstance(voidType, stringType, regexOptionsType))),
				ScanMethod(regexType.FindMethod(".ctor", MethodSig.CreateInstance(voidType, stringType, regexOptionsType, timeSpanType))),

				ScanMethod(regexType.FindMethod("IsMatch", MethodSig.CreateStatic(boolType, stringType, stringType))),
				ScanMethod(regexType.FindMethod("IsMatch", MethodSig.CreateStatic(boolType, stringType, stringType, regexOptionsType))),
				ScanMethod(regexType.FindMethod("IsMatch", MethodSig.CreateStatic(boolType, stringType, stringType, regexOptionsType, timeSpanType))),

				ScanMethod(regexType.FindMethod("Match", MethodSig.CreateStatic(regexMatchType, stringType, stringType))),
				ScanMethod(regexType.FindMethod("Match", MethodSig.CreateStatic(regexMatchType, stringType, stringType, regexOptionsType))),
				ScanMethod(regexType.FindMethod("Match", MethodSig.CreateStatic(regexMatchType, stringType, stringType, regexOptionsType, timeSpanType))),

				ScanMethod(regexType.FindMethod("Matches", MethodSig.CreateStatic(regexMatchColType, stringType, stringType))),
				ScanMethod(regexType.FindMethod("Matches", MethodSig.CreateStatic(regexMatchColType, stringType, stringType, regexOptionsType))),
				ScanMethod(regexType.FindMethod("Matches", MethodSig.CreateStatic(regexMatchColType, stringType, stringType, regexOptionsType, timeSpanType))),

				ScanMethod(regexType.FindMethod("Split", MethodSig.CreateStatic(stringArrType, stringType, stringType))),
				ScanMethod(regexType.FindMethod("Split", MethodSig.CreateStatic(stringArrType, stringType, stringType, regexOptionsType))),
				ScanMethod(regexType.FindMethod("Split", MethodSig.CreateStatic(stringArrType, stringType, stringType, regexOptionsType, timeSpanType))),

				ScanMethod(regexType.FindMethod("Replace", MethodSig.CreateStatic(stringType, stringType, stringType, stringType))),
				ScanMethod(regexType.FindMethod("Replace", MethodSig.CreateStatic(stringType, stringType, stringType, stringType, regexOptionsType))),
				ScanMethod(regexType.FindMethod("Replace", MethodSig.CreateStatic(stringType, stringType, stringType, stringType, regexOptionsType, timeSpanType))),

				ScanMethod(regexType.FindMethod("Replace", MethodSig.CreateStatic(stringType, stringType, stringType, regexMatchEvalType))),
				ScanMethod(regexType.FindMethod("Replace", MethodSig.CreateStatic(stringType, stringType, stringType, regexMatchEvalType, regexOptionsType))),
				ScanMethod(regexType.FindMethod("Replace", MethodSig.CreateStatic(stringType, stringType, stringType, regexMatchEvalType, regexOptionsType, timeSpanType)))
			).RemoveAll(m => m == null);
		}

		public IRegexTargetMethod GetMatchingMethod(IMethod method) {
			if (method == null) throw new ArgumentNullException(nameof(method));

			if (method.DeclaringType.FullName != CompileRegexProtection._RegexTypeFullName) return null;

			var sig = new SigComparer();
			foreach (var testMethod in Methods) {
				if (testMethod.Method.Name == method.Name && sig.Equals(testMethod.Method.MethodSig, method.MethodSig))
					return testMethod;
			}
			return null;
		}

		private static RegexTargetMethod ScanMethod(MethodDef method) {
			if (method == null) return null;

			var patternIndex = method.Parameters.Where(p => p.Name == "pattern").Select(p => p.Index).First();
			var optionsIndex = method.Parameters.Where(p => p.Name == "options").Select(p => p.Index).DefaultIfEmpty(-1).First();
			var timeoutIndex = method.Parameters.Where(p => p.Name == "matchTimeout").Select(p => p.Index).DefaultIfEmpty(-1).First();

			MethodDef equivalent = null;
			if (!method.IsInstanceConstructor) {
				var searchSig = MethodSig.CreateInstance(method.MethodSig.RetType);
				for (int i = 0; i < method.MethodSig.Params.Count; i++) {
					if (i != patternIndex && i != optionsIndex && i != timeoutIndex)
						searchSig.Params.Add(method.MethodSig.Params[i]);
				}
				equivalent = method.DeclaringType.FindMethod(method.Name, searchSig);
			}

			return new RegexTargetMethod(method, equivalent, patternIndex, optionsIndex, timeoutIndex);
		}

		private sealed class RegexTargetMethod : IRegexTargetMethod, IEquatable<RegexTargetMethod> {
			public MethodDef Method { get; }
			public MethodDef InstanceEquivalentMethod { get; }
			public int PatternParameterIndex { get; }
			public int OptionsParameterIndex { get; }
			public int TimeoutParameterIndex { get; }

			internal RegexTargetMethod(MethodDef method, MethodDef equivalentMethod, int pattern, int options, int timeout) {
				Method = method ?? throw new ArgumentNullException(nameof(method));
				InstanceEquivalentMethod = equivalentMethod;
				if (!method.IsInstanceConstructor && equivalentMethod == null)
					throw new ArgumentNullException(nameof(equivalentMethod));
				PatternParameterIndex = pattern;
				OptionsParameterIndex = options;
				TimeoutParameterIndex = timeout;
			}

			public bool Equals(RegexTargetMethod other) => other != null && Method.Equals(other.Method);

			public bool Equals(IRegexTargetMethod other) => Equals(other as RegexTargetMethod);

			public override bool Equals(object obj) => Equals(obj as RegexTargetMethod);

			public override int GetHashCode() => Method.GetHashCode();
		}
	}
}
