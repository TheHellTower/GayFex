using System;
using System.Diagnostics.CodeAnalysis;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Optimizations.TailCall {
	[SuppressMessage("ReSharper", "StringLiteralTypo")]
	internal static class LoggerExtensions {
		private static readonly Action<ILogger, MethodDef, Exception> _scanningMethodForTailCall = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20101, "opti-101"), "Inspecting {Method} for tail calls.");
		internal static void LogMsgScanningForTailCall(this ILogger logger, MethodDef method) =>
			_scanningMethodForTailCall(logger, method, null);

		private static readonly Action<ILogger, MethodDef, uint, Exception> _foundTailCallInMethod = LoggerMessage.Define<MethodDef, uint>(
			LogLevel.Debug, new EventId(20102, "opti-102"), "Found tail call in {Method} at instruction IL_{Offset:X4}");
		internal static void LogMsgFoundTailCallInMethod(this ILogger logger, MethodDef method, Instruction instruction) =>
			_foundTailCallInMethod(logger, method, instruction.GetOffset(), null);

		private static readonly Action<ILogger, int, Exception> _totalInjectedTailCalls = LoggerMessage.Define<int>(
			LogLevel.Information, new EventId(20103, "opti-103"), "Optimized {Count} tail calls.");
		internal static void LogMsgTotalInjectedTailCalls(this ILogger logger, int count) =>
			_totalInjectedTailCalls(logger, count, null);

		private static readonly Action<ILogger, MethodDef, Exception> _scanningMethodForTailRecursion = LoggerMessage.Define<MethodDef>(
			LogLevel.Trace, new EventId(20111, "opti-111"), "Inspecting {Method} for tail recursions.");
		internal static void LogMsgScanningForTailRecursion(this ILogger logger, MethodDef method) =>
			_scanningMethodForTailRecursion(logger, method, null);

		private static readonly Action<ILogger, MethodDef, uint, Exception> _foundTailRecursionInMethod = LoggerMessage.Define<MethodDef, uint>(
			LogLevel.Debug, new EventId(20112, "opti-112"), "Found tail recursion in {Method} at instruction IL_{Offset:X4}");
		internal static void LogMsgFoundTailRecursionInMethod(this ILogger logger, MethodDef method, Instruction instruction) =>
			_foundTailRecursionInMethod(logger, method, instruction.GetOffset(), null);

		private static readonly Action<ILogger, int, Exception> _totalInjectedTailRecursions = LoggerMessage.Define<int>(
			LogLevel.Information, new EventId(20113, "opti-113"), "Optimized {Count} tail recursions.");
		internal static void LogMsgTotalInjectedTailRecursions(this ILogger logger, int count) =>
			_totalInjectedTailRecursions(logger, count, null);
	}
}
