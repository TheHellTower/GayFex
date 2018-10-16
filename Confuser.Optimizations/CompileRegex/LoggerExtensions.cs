using System;
using Confuser.Optimizations.CompileRegex.Compiler;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Optimizations.CompileRegex {
	internal static class LoggerExtensions {
		private static readonly Action<ILogger, ModuleDef, Exception> _InspectingModule = LoggerMessage.Define<ModuleDef>(
			LogLevel.Debug, new EventId(20001, "opti-1"), "Inspecting {0} for regular expressions reference.");
		internal static void LogMsgInspectingModule(this ILogger logger, ModuleDef module) =>
			_InspectingModule(logger, module, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _RegexReferencesFound = LoggerMessage.Define<ModuleDef>(
			LogLevel.Trace, new EventId(20002, "opti-2"), "Found regular expression references in {0}.");
		internal static void LogMsgRegexReferencesFound(this ILogger logger, ModuleDef module) =>
			_RegexReferencesFound(logger, module, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _NoRegexReferencesFound = LoggerMessage.Define<ModuleDef>(
			LogLevel.Trace, new EventId(20003, "opti-3"), "No regular expression references found in {0}.");
		internal static void LogMsgNoRegexReferencesFound(this ILogger logger, ModuleDef module) =>
			_NoRegexReferencesFound(logger, module, null);

		private static readonly Action<ILogger, MethodDef, Exception> _ExtracingFromMethod = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20004, "opti-4"), "Trying to extract regular expressions from the method {0}.");
		internal static void LogMsgExtractFromMethod(this ILogger logger, MethodDef method) =>
			_ExtracingFromMethod(logger, method, null);

		private static readonly Action<ILogger, MethodDef, MethodDef, Exception> _FoundRegexReferenceInMethod = LoggerMessage.Define<MethodDef, MethodDef>(
			LogLevel.Debug, new EventId(20005, "opti-5"), "Found reference to regular expression in method {0}: {1}");
		internal static void LogMsgFoundRegexReferenceInMethod(this ILogger logger, MethodDef method, IRegexTargetMethod targetMethod) =>
			_FoundRegexReferenceInMethod(logger, method, targetMethod.Method, null);

		private static readonly Action<ILogger, ModuleDef, int, Exception> _regexCompilingForModule = LoggerMessage.Define<ModuleDef, int>(
			LogLevel.Debug, new EventId(20006, "opti-6"), "Compiling {1:d} expressions in module {0}");
		internal static void LogMsgRegexCompilingForModule(this ILogger logger, ModuleDef module, int expressionCount) =>
			_regexCompilingForModule(logger, module, expressionCount, null);

		private static readonly Action<ILogger, TypeDef, Exception> _regexFinishedCompiling = LoggerMessage.Define<TypeDef>(
			LogLevel.Trace, new EventId(20007, "opti-7"), "Compiled regex expression to: {0}");
		internal static void LogMsgRegexFinishedCompiling(this ILogger logger, RegexCompilerResult compilerResult) =>
			_regexFinishedCompiling(logger, compilerResult.RegexTypeDef, null);
	}
}
