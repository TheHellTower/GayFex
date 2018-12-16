using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Optimizations.TailCall {
	internal static class LoggerExtensions {
		private static readonly Action<ILogger, MethodDef, Exception> _ScanningMethodForTailCall = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20101, "opti-101"), "Inspecting {Method} for tail calls.");
		internal static void LogMsgScanningForTailCall(this ILogger logger, MethodDef method) =>
			_ScanningMethodForTailCall(logger, method, null);

		private static readonly Action<ILogger, MethodDef, uint, Exception> _FoundTailCallInMethod = LoggerMessage.Define<MethodDef, uint>(
			LogLevel.Debug, new EventId(20102, "opti-102"), "Found tail call in {Method} at instruction IL_{Offset:X4}");
		internal static void LogMsgFoundTailCallInMethod(this ILogger logger, MethodDef method, Instruction instruction) =>
			_FoundTailCallInMethod(logger, method, instruction.GetOffset(), null);

		private static readonly Action<ILogger, int, Exception> _TotalInjectedTailCalls = LoggerMessage.Define<int>(
			LogLevel.Information, new EventId(20103, "opti-103"), "Optimized {Count} tail calls.");
		internal static void LogMsgTotalInjectedTailCalls(this ILogger logger, int count) =>
			_TotalInjectedTailCalls(logger, count, null);

		private static readonly Action<ILogger, MethodDef, Exception> _ScanningMethodForTailRecursion = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20111, "opti-111"), "Inspecting {Method} for tail recursions.");
		internal static void LogMsgScanningForTailRecursion(this ILogger logger, MethodDef method) =>
			_ScanningMethodForTailRecursion(logger, method, null);

		private static readonly Action<ILogger, MethodDef, uint, Exception> _FoundTailRecursionInMethod = LoggerMessage.Define<MethodDef, uint>(
			LogLevel.Debug, new EventId(20112, "opti-112"), "Found tail recursion in {Method} at instruction IL_{Offset:X4}");
		internal static void LogMsgFoundTailRecursionInMethod(this ILogger logger, MethodDef method, Instruction instruction) =>
			_FoundTailRecursionInMethod(logger, method, instruction.GetOffset(), null);

		private static readonly Action<ILogger, int, Exception> _TotalInjectedTailRecursions = LoggerMessage.Define<int>(
			LogLevel.Information, new EventId(20113, "opti-113"), "Optimized {Count} tail recursions.");
		internal static void LogMsgTotalInjectedTailRecursions(this ILogger logger, int count) =>
			_TotalInjectedTailRecursions(logger, count, null);
	}
}
