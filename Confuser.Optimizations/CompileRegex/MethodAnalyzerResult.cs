using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex {
	internal struct MethodAnalyzerResult {
		internal Instruction MainInstruction { get; set; }

		internal Instruction PatternInstruction { get; set; }
		internal Instruction OptionsInstruction { get; set; }
		internal IList<Instruction> TimeoutInstructions { get; set; }

		internal IRegexTargetMethod RegexMethod { get; set; }
		internal RegexCompileDef CompileDef { get; set; }

		internal bool ExplicitCompiled { get; set; }
	}
}
