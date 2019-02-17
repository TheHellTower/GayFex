using System;
using System.Diagnostics.CodeAnalysis;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	[SuppressMessage("ReSharper", "StringLiteralTypo")]
	internal sealed class RegexRunnerDef {
		internal ModuleDef RegexModule { get; }

		internal TypeDef RegexRunnerTypeDef { get; }

		internal MethodDef CheckTimeoutMethodDef { get; }

		internal RegexRunnerDef(ModuleDef regexModule) {
			RegexModule = regexModule ?? throw new ArgumentNullException(nameof(regexModule));

			RegexRunnerTypeDef = regexModule.FindThrow(CompileRegexProtection._RegexNamespace + ".RegexRunner", false);
			CheckTimeoutMethodDef = RegexRunnerTypeDef.FindMethod("CheckTimeout");
		}
	}
}
