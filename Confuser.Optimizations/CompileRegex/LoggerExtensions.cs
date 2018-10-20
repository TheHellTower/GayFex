using System;
using Confuser.Optimizations.CompileRegex.Compiler;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Optimizations.CompileRegex {
	internal static class LoggerExtensions {
		private static readonly Action<ILogger, ModuleDef, Exception> _InspectingModule = LoggerMessage.Define<ModuleDef>(
			LogLevel.Debug, new EventId(20001, "opti-1"), "Inspecting {Module} for regular expressions reference.");
		internal static void LogMsgInspectingModule(this ILogger logger, ModuleDef module) =>
			_InspectingModule(logger, module, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _RegexReferencesFound = LoggerMessage.Define<ModuleDef>(
			LogLevel.Trace, new EventId(20002, "opti-2"), "Found regular expression references in {Module}.");
		internal static void LogMsgRegexReferencesFound(this ILogger logger, ModuleDef module) =>
			_RegexReferencesFound(logger, module, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _NoRegexReferencesFound = LoggerMessage.Define<ModuleDef>(
			LogLevel.Trace, new EventId(20003, "opti-3"), "No regular expression references found in {Module}.");
		internal static void LogMsgNoRegexReferencesFound(this ILogger logger, ModuleDef module) =>
			_NoRegexReferencesFound(logger, module, null);

		private static readonly Action<ILogger, MethodDef, Exception> _ExtracingFromMethod = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20004, "opti-4"), "Trying to extract regular expressions from the method {Method}.");
		internal static void LogMsgExtractFromMethod(this ILogger logger, MethodDef method) =>
			_ExtracingFromMethod(logger, method, null);

		private static readonly Action<ILogger, MethodDef, MethodDef, Exception> _FoundRegexReferenceInMethod = LoggerMessage.Define<MethodDef, MethodDef>(
			LogLevel.Debug, new EventId(20005, "opti-5"), "Found reference to regular expression in method {ScannedMethod}: {RegexMethod}");
		internal static void LogMsgFoundRegexReferenceInMethod(this ILogger logger, MethodDef method, IRegexTargetMethod targetMethod) =>
			_FoundRegexReferenceInMethod(logger, method, targetMethod.Method, null);

		private static readonly Action<ILogger, MethodDef, Exception> _SkippedRegexNotCompiled = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20006, "opti-6"), "Skipped the regex call in {Method}, because it is not marked with RegexOptions.Compiled.");
		internal static void LogMsgSkippedRegexNotCompiled(this ILogger logger, MethodDef method) =>
			_SkippedRegexNotCompiled(logger, method, null);

		private static readonly Action<ILogger, int, ModuleDef, Exception> _regexCompilingForModule = LoggerMessage.Define<int, ModuleDef>(
			LogLevel.Debug, new EventId(20007, "opti-7"), "Compiling {Count} expressions in module {Module}");
		internal static void LogMsgRegexCompilingForModule(this ILogger logger, ModuleDef module, int expressionCount) =>
			_regexCompilingForModule(logger, expressionCount, module, null);

		private static readonly Action<ILogger, TypeDef, Exception> _regexFinishedCompiling = LoggerMessage.Define<TypeDef>(
			LogLevel.Trace, new EventId(20008, "opti-8"), "Compiled regex expression to: {TypeDef}");
		internal static void LogMsgRegexFinishedCompiling(this ILogger logger, RegexCompilerResult compilerResult) =>
			_regexFinishedCompiling(logger, compilerResult.RegexTypeDef, null);

		private static readonly Action<ILogger, string, Exception> _regexSkippedBroken = LoggerMessage.Define<string>(
			LogLevel.Warning, new EventId(20009, "opti-9"), "Skipping broken expression: {Pattern}");
		internal static void LogMsgRegexSkippedBrokenExpression(this ILogger logger, RegexCompileDef compileDef) =>
			_regexSkippedBroken(logger, compileDef.Pattern, null);

		private static readonly Action<ILogger, Exception> _regexInvalidPattern = LoggerMessage.Define(
			LogLevel.Critical, new EventId(20010, "opti-10"), "Invalid regex pattern found.");
		internal static void LogMsgInvalidRegexPatternFound(this ILogger logger, RegexCompilerException ex) =>
			_regexInvalidPattern(logger, ex);

		private static readonly Action<ILogger, string, Exception> _regexSkippedUnsafe = LoggerMessage.Define<string>(
			LogLevel.Debug, new EventId(20011, "opti-11"), "Skipped compilation of culture unsafe expression: {Pattern}");
		internal static void LogMsgSkippedUnsafe(this ILogger logger, RegexCompileDef compileDef) =>
			_regexSkippedUnsafe(logger, compileDef.Pattern, null);
	}
}
