using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Optimizations.Services {
	public interface ICompileRegexService {
		IRegexTargetMethods GetRegexTargetMethods(ModuleDef module);

		bool AnalyzeModule(ModuleDef module);

		void RecordExpression(ModuleDef module, RegexCompileDef compileDef, IRegexTargetMethod regexMethod);

		IEnumerable<RegexCompileDef> GetExpressions(ModuleDef module);
	}
}
