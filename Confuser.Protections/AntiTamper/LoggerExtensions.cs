using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.AntiTamper {
	internal static class LoggerExtensions {
		private static readonly Action<ILogger, ModuleDef, Exception> _normalModeStart = LoggerMessage.Define<ModuleDef>(
			LogLevel.Debug, new EventId(101, "prot-101"), "Normal anti tamper protection processing {module}.");
		internal static void LogMsgNormalModeStart(this ILogger logger, ModuleDef moduleDef) => _normalModeStart(logger, moduleDef, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _normalModeInjectStart = LoggerMessage.Define<ModuleDef>(
			LogLevel.Trace, new EventId(102, "prot-102"), "Normal anti tamper protection injecting runtime into {module}");
		internal static void LogMsgNormalModeInjectStart(this ILogger logger, ModuleDef moduleDef) => _normalModeInjectStart(logger, moduleDef, null);

		private static readonly Action<ILogger, Exception> _normalModeRuntimeMissing = LoggerMessage.Define(
			LogLevel.Warning, new EventId(103, "prot-103"), "Runtime implementation for \"Normal\" anti tamper protection not found.");
		internal static void LogMsgNormalModeRuntimeMissing(this ILogger logger) => _normalModeRuntimeMissing(logger, null);

		private static readonly Action<ILogger, ModuleDef, Exception> _normalModeInjectDone = LoggerMessage.Define<ModuleDef>(
			LogLevel.Trace, new EventId(104, "prot-104"), "Normal anti tamper protection runtime injection into {module} done.");
		internal static void LogMsgNormalModeInjectDone(this ILogger logger, ModuleDef moduleDef) => _normalModeInjectDone(logger, moduleDef, null);
	}
}
