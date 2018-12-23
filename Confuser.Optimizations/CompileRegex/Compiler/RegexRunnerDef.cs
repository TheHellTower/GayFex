using System;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexRunnerDef {
		internal ModuleDef RegexModule { get; }

		internal TypeDef RegexRunnerTypeDef { get; }

#pragma warning disable IDE1006
		internal FieldDef runtextbegFieldDef { get; }
		internal FieldDef runtextendFieldDef { get; }
		internal FieldDef runtextstartFieldDef { get; }
		internal FieldDef runtextposFieldDef { get; }
		internal FieldDef runtextFieldDef { get; }
		internal FieldDef runtrackposFieldDef { get; }
		internal FieldDef runtrackFieldDef { get; }
		internal FieldDef runstackposFieldDef { get; }
		internal FieldDef runstackFieldDef { get; }
		internal FieldDef runtrackcountFieldDef { get; }
#pragma warning restore IDE1006

		internal MethodDef EnsureStorageMethodDef { get; }
		internal MethodDef CaptureMethodDef { get; }
		internal MethodDef TransferCaptureMethodDef { get; }
		internal MethodDef UncaptureMethodDef { get; }
		internal MethodDef IsMatchedMethodDef { get; }
		internal MethodDef MatchLengthMethodDef { get; }
		internal MethodDef MatchIndexMethodDef { get; }
		internal MethodDef IsBoundaryMethodDef { get; }
		internal MethodDef CharInClassMethodDef { get; }
		internal MethodDef IsECMABoundaryMethodDef { get; }
		internal MethodDef CrawlposMethodDef { get; }
		internal MethodDef CheckTimeoutMethodDef { get; }

		internal RegexRunnerDef(ModuleDef regexModule) {
			RegexModule = regexModule ?? throw new ArgumentNullException(nameof(regexModule));

			RegexRunnerTypeDef = regexModule.FindThrow(CompileRegexProtection._RegexNamespace + ".RegexRunner", false);

			runtextbegFieldDef = RegexRunnerTypeDef.FindField("runtextbeg");
			runtextendFieldDef = RegexRunnerTypeDef.FindField("runtextend");
			runtextstartFieldDef = RegexRunnerTypeDef.FindField("runtextstart");
			runtextposFieldDef = RegexRunnerTypeDef.FindField("runtextpos");
			runtextFieldDef = RegexRunnerTypeDef.FindField("runtext");
			runtrackposFieldDef = RegexRunnerTypeDef.FindField("runtrackpos");
			runtrackFieldDef = RegexRunnerTypeDef.FindField("runtrack");
			runstackposFieldDef = RegexRunnerTypeDef.FindField("runstackpos");
			runstackFieldDef = RegexRunnerTypeDef.FindField("runstack");
			runtrackcountFieldDef = RegexRunnerTypeDef.FindField("runtrackcount");

			EnsureStorageMethodDef = RegexRunnerTypeDef.FindMethod("EnsureStorage");
			CaptureMethodDef = RegexRunnerTypeDef.FindMethod("Capture");
			TransferCaptureMethodDef = RegexRunnerTypeDef.FindMethod("TransferCapture");
			UncaptureMethodDef = RegexRunnerTypeDef.FindMethod("Uncapture");
			IsMatchedMethodDef = RegexRunnerTypeDef.FindMethod("IsMatched");
			MatchLengthMethodDef = RegexRunnerTypeDef.FindMethod("MatchLength");
			MatchIndexMethodDef = RegexRunnerTypeDef.FindMethod("MatchIndex");
			IsBoundaryMethodDef = RegexRunnerTypeDef.FindMethod("IsBoundary");
			CharInClassMethodDef = RegexRunnerTypeDef.FindMethod("CharInClass");
			IsECMABoundaryMethodDef = RegexRunnerTypeDef.FindMethod("IsECMABoundary");
			CrawlposMethodDef = RegexRunnerTypeDef.FindMethod("Crawlpos");
			CheckTimeoutMethodDef = RegexRunnerTypeDef.FindMethod("CheckTimeout");
		}
	}
}
