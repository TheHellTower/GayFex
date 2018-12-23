using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Confuser.Optimizations.CompileRegex.Compiler;
using Confuser.Optimizations.Services;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex {
	internal sealed class CompileRegexService : ICompileRegexService {
		private IDictionary<ModuleDef, IRegexTargetMethods> CachedRegexTargetMethods { get; }
		private IDictionary<ModuleDef, IDictionary<(String pattern, RegexOptions options), (TimeSpan? timeout, bool staticTimeout, ISet<IRegexTargetMethod> requiredMethods)>> RecordedRegex { get; }
		private IDictionary<ModuleDef, IList<RegexCompilerResult>> CompiledRegexExpressions { get; }

		internal CompileRegexService() {
			CachedRegexTargetMethods = new Dictionary<ModuleDef, IRegexTargetMethods>();
			RecordedRegex = new Dictionary<ModuleDef, IDictionary<(String, RegexOptions), (TimeSpan?, bool, ISet<IRegexTargetMethod>)>>();
			CompiledRegexExpressions = new Dictionary<ModuleDef, IList<RegexCompilerResult>>();
		}

		IRegexTargetMethods ICompileRegexService.GetRegexTargetMethods(ModuleDef module) {
			if (module == null) throw new ArgumentNullException(nameof(module));

			if (CachedRegexTargetMethods.TryGetValue(module, out var result)) {
				return result;
			}
			return null;
		}

		bool ICompileRegexService.AnalyzeModule(ModuleDef module) {
			if (module == null) throw new ArgumentNullException(nameof(module));

			return GetOrCreateRegexTargetMethods(module) != null;
		}

		private IRegexTargetMethods GetOrCreateRegexTargetMethods(ModuleDef moduleDef) {
			Debug.Assert(moduleDef != null, $"{nameof(moduleDef)} != null");

			if (!CachedRegexTargetMethods.TryGetValue(moduleDef, out var result)) {
				var regexTypeRef = moduleDef.GetTypeRefs()
					.Where(t => t.FullName == CompileRegexProtection._RegexTypeFullName)
					.FirstOrDefault();

				if (regexTypeRef != null) {
					var regexTypeDef = regexTypeRef.Resolve();
					if (regexTypeDef != null)
						result = new RegexTargetMethods(regexTypeDef);
				}
				CachedRegexTargetMethods.Add(moduleDef, result);
			}
			return result;
		}

		void ICompileRegexService.RecordExpression(ModuleDef module, RegexCompileDef compileDef, IRegexTargetMethod regexMethod) {
			if (module == null) throw new ArgumentNullException(nameof(module));
			if (compileDef == null) throw new ArgumentNullException(nameof(compileDef));
			if (regexMethod == null) throw new ArgumentNullException(nameof(regexMethod));

			if (!RecordedRegex.TryGetValue(module, out var recordedRegexByModule)) {
				recordedRegexByModule = new Dictionary<(String, RegexOptions), (TimeSpan?, bool, ISet<IRegexTargetMethod>)>();
				RecordedRegex.Add(module, recordedRegexByModule);
			}

			var regexKey = (compileDef.Pattern, compileDef.Options);
			if (!recordedRegexByModule.TryGetValue(regexKey, out var regexParams)) {
				ISet<IRegexTargetMethod> requiredMethods = new HashSet<IRegexTargetMethod>() { regexMethod };
				recordedRegexByModule.Add(regexKey, (compileDef.Timeout, compileDef.StaticTimeout, requiredMethods));
			} else {
				regexParams.requiredMethods.Add(regexMethod);
				if (!compileDef.StaticTimeout || !regexParams.staticTimeout || !Nullable.Equals(compileDef.Timeout, regexParams.timeout)) {
					regexParams = (null, false, regexParams.requiredMethods);
					recordedRegexByModule[regexKey] = regexParams;
				}
			}
		}

		IEnumerable<RegexCompileDef> ICompileRegexService.GetExpressions(ModuleDef module) {
			if (module == null) throw new ArgumentNullException(nameof(module));

			if (RecordedRegex.TryGetValue(module, out var recordedRegexExpressions)) {
				foreach (var entry in recordedRegexExpressions)
					yield return new RegexCompileDef(entry.Key.pattern, entry.Key.options, entry.Value.timeout, entry.Value.staticTimeout, entry.Value.requiredMethods);
			}
		}

		internal void AddCompiledRegex(ModuleDef module, RegexCompilerResult result) {
			if (module == null) throw new ArgumentNullException(nameof(module));
			if (result == null) throw new ArgumentNullException(nameof(result));

			if (!CompiledRegexExpressions.TryGetValue(module, out var resultList))
				resultList = CompiledRegexExpressions[module] = new List<RegexCompilerResult>();

			resultList.Add(result);
		}

		internal RegexCompilerResult GetCompiledRegex(ModuleDef module, RegexCompileDef compileDef) {
			if (module == null) throw new ArgumentNullException(nameof(module));

			if (CompiledRegexExpressions.TryGetValue(module, out var resultList)) {
				foreach (var result in resultList) {
					if (result.CompileDef == compileDef)
						return result;
				}
			}

			return null;
		}
	}
}
