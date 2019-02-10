using System;
using Confuser.Optimizations.CompileRegex.Compiler;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Optimizations.CompileRegex {
	internal static class LoggerExtensions {
		private static readonly Action<ILogger, ModuleDef, Exception> _inspectingModule =
			LoggerMessage.Define<ModuleDef>(
				LogLevel.Debug, new EventId(20001, "opti-1"), "Inspecting {Module} for regular expressions reference.");

		internal static void LogMsgInspectingModule(this ILogger logger, ModuleDef module) =>
			_inspectingModule(logger, module, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _regexReferencesFound =
			LoggerMessage.Define<ModuleDef>(
				LogLevel.Trace, new EventId(20002, "opti-2"), "Found regular expression references in {Module}.");

		internal static void LogMsgRegexReferencesFound(this ILogger logger, ModuleDef module) =>
			_regexReferencesFound(logger, module, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _noRegexReferencesFound =
			LoggerMessage.Define<ModuleDef>(
				LogLevel.Trace, new EventId(20003, "opti-3"), "No regular expression references found in {Module}.");

		internal static void LogMsgNoRegexReferencesFound(this ILogger logger, ModuleDef module) =>
			_noRegexReferencesFound(logger, module, null);

		private static readonly Action<ILogger, MethodDef, Exception> _extracingFromMethod =
			LoggerMessage.Define<MethodDef>(
				LogLevel.Trace, new EventId(20004, "opti-4"),
				"Trying to extract regular expressions from the method {Method}.");

		internal static void LogMsgExtractFromMethod(this ILogger logger, MethodDef method) =>
			_extracingFromMethod(logger, method, null);

		private static readonly Action<ILogger, MethodDef, MethodDef, Exception> _foundRegexReferenceInMethod =
			LoggerMessage.Define<MethodDef, MethodDef>(
				LogLevel.Debug, new EventId(20005, "opti-5"),
				"Found reference to regular expression in method {ScannedMethod}: {RegexMethod}");

		internal static void LogMsgFoundRegexReferenceInMethod(this ILogger logger, MethodDef method,
			IRegexTargetMethod targetMethod) =>
			_foundRegexReferenceInMethod(logger, method, targetMethod.Method, null);

		private static readonly Action<ILogger, MethodDef, Exception> _skippedRegexNotCompiled =
			LoggerMessage.Define<MethodDef>(
				LogLevel.Trace, new EventId(20006, "opti-6"),
				"Skipped the RegEx call in {Method}, because it is not marked with RegexOptions.Compiled.");

		internal static void LogMsgSkippedRegexNotCompiled(this ILogger logger, MethodDef method) =>
			_skippedRegexNotCompiled(logger, method, null);

		private static readonly Action<ILogger, int, ModuleDef, Exception> _regexCompilingForModule =
			LoggerMessage.Define<int, ModuleDef>(
				LogLevel.Debug, new EventId(20007, "opti-7"), "Compiling {Count} expressions in module {Module}");

		internal static void
			LogMsgRegexCompilingForModule(this ILogger logger, ModuleDef module, int expressionCount) =>
			_regexCompilingForModule(logger, expressionCount, module, null);

		private static readonly Action<ILogger, TypeDef, Exception> _regexFinishedCompiling =
			LoggerMessage.Define<TypeDef>(
				LogLevel.Trace, new EventId(20008, "opti-8"), "Compiled regular expression to: {TypeDef}");

		internal static void LogMsgRegexFinishedCompiling(this ILogger logger, RegexCompilerResult compilerResult) =>
			_regexFinishedCompiling(logger, compilerResult.RegexTypeDef, null);

		private static readonly Action<ILogger, string, Exception> _regexSkippedBroken = LoggerMessage.Define<string>(
			LogLevel.Warning, new EventId(20009, "opti-9"), "Skipping broken expression: {Pattern}");

		internal static void LogMsgRegexSkippedBrokenExpression(this ILogger logger, RegexCompileDef compileDef) =>
			_regexSkippedBroken(logger, compileDef.Pattern, null);

		private static readonly Action<ILogger, Exception> _regexInvalidPattern = LoggerMessage.Define(
			LogLevel.Critical, new EventId(20010, "opti-10"), "Invalid regular expression pattern found.");

		internal static void LogMsgInvalidRegexPatternFound(this ILogger logger, RegexCompilerException ex) =>
			_regexInvalidPattern(logger, ex);

		private static readonly Action<ILogger, string, Exception> _regexSkippedUnsafe = LoggerMessage.Define<string>(
			LogLevel.Debug, new EventId(20011, "opti-11"),
			"Skipped compilation of culture unsafe expression: {Pattern}");

		internal static void LogMsgSkippedUnsafe(this ILogger logger, RegexCompileDef compileDef) =>
			_regexSkippedUnsafe(logger, compileDef.Pattern, null);

		private static readonly Action<ILogger, string, MethodDef, Exception> _noMatchingTargetMethod =
			LoggerMessage.Define<string, MethodDef>(
				LogLevel.Warning, new EventId(20012, "opti-12"),
				"The regular expression \"{Pattern}\" was compiled, but the required target method for \"{TargetMethod}\" was not found.");

		internal static void LogMsgNoMatchingTargetMethod(this ILogger logger, IRegexTargetMethod targetMethod,
			RegexCompilerResult compilerResult) =>
			_noMatchingTargetMethod(logger, compilerResult.CompileDef.Pattern, targetMethod.Method, null);

		private static readonly Action<ILogger, string, MethodDef, Exception> _injectionDone =
			LoggerMessage.Define<string, MethodDef>(
				LogLevel.Trace, new EventId(20013, "opti-13"),
				"The compiled regular expression \"{Pattern}\", was injected into \"{InjectionTargetMethod}\".");

		internal static void LogMsgInjectSuccessful(this ILogger logger, RegexCompilerResult compilerResult,
			MethodDef targetMethod) =>
			_injectionDone(logger, compilerResult.CompileDef.Pattern, targetMethod, null);

		private static readonly Action<ILogger, int, ModuleDef, Exception> _compileSummary =
			LoggerMessage.Define<int, ModuleDef>(
				LogLevel.Information, new EventId(20014, "opti-14"),
				"Compiled {Count} regular expressions in module \"{Module}\".");

		internal static void LogMsgCompileSummary(this ILogger logger, int count, ModuleDef module) =>
			_compileSummary(logger, count, module, null);
	}
}
