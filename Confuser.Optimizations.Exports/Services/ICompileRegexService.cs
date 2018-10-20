using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Optimizations.Services {
	public interface ICompileRegexService {
		IRegexTargetMethods GetRegexTargetMethods(ModuleDef moduleDef);

		bool AnalyzeModule(ModuleDef moduleDef);

		void RecordExpression(ModuleDef moduleDef, RegexCompileDef compileDef, IRegexTargetMethod regexMethod);

		IEnumerable<RegexCompileDef> GetExpressions(ModuleDef moduleDef);
	}
}
