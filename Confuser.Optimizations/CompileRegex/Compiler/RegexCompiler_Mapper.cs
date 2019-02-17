using System;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal partial class RegexCompiler {
		private class Mapper : ImportMapper {
			private ModuleDef TargetModule { get; }
			private RegexRunnerDef RunnerDef { get; }

			internal Mapper(ModuleDef targetModule, RegexRunnerDef runnerDef) {
				TargetModule = targetModule ?? throw new ArgumentNullException(nameof(targetModule));
				RunnerDef = runnerDef ?? throw new ArgumentNullException(nameof(runnerDef));
			}

			public override TypeRef Map(Type source) {
				var mappedRef = MapReference(source.Namespace, source.FullName);
				return mappedRef ?? base.Map(source);
			}

			private TypeRef MapReference(string ns, string fullname) {
				if (string.Equals(ns, CompileRegexProtection._RegexNamespace, StringComparison.Ordinal))
					return TargetModule.Import(RunnerDef.RegexModule.FindThrow(fullname, false));

				return RunnerDef.RegexModule.GetTypeRefs().FirstOrDefault(tr =>
					string.Equals(tr.FullName, fullname, StringComparison.Ordinal));
			}
		}
	}
}
