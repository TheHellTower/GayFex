using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex {
	internal struct MethodAnalyzerResult {
		internal Instruction mainInstruction;

		internal Instruction patternInstr;
		internal Instruction optionsInstr;
		internal IList<Instruction> timeoutInstrs;

		internal IRegexTargetMethod regexMethod;
		internal RegexCompileDef compileDef;

		internal bool explicitCompiled;
	}
}
