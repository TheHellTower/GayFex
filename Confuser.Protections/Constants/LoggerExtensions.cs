using System;
using System.Globalization;
using Confuser.Core.Services;
using dnlib.DotNet;
using Humanizer;
using Humanizer.Bytes;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Protections.Constants {
	internal static class LoggerExtensions {
		private const int BaseId = 10100;
		private const string BaseStr = "prot-1";

        private static CultureInfo Culture => CultureInfo.CurrentCulture;

		private static readonly Action<ILogger, MethodDef, object, Exception> _foundConstantValue =
			LoggerMessage.Define<MethodDef, object>(
				LogLevel.Trace, CreateEventId(1), "Found constant value in method {Method}: {Value}");

		internal static void LogMsgFoundConstantValue(this ILogger logger, MethodDef methodDef, object value) =>
			_foundConstantValue(logger, methodDef, value, null);

		private static readonly Action<ILogger, MethodDef, string, Exception> _foundConstantInitializer =
			LoggerMessage.Define<MethodDef, string>(
				LogLevel.Trace, CreateEventId(2), "Found constant initializer in method {Method}: {Length}");

		internal static void LogMsgFoundConstantInitializer(this ILogger logger, MethodDef methodDef, byte[] data) =>
			_foundConstantInitializer(logger, methodDef, data.Length.Bytes().Humanize("0.00", Culture), null);
		
		private static readonly Action<ILogger, MethodDef, Exception> _extractingFromMethod =
			LoggerMessage.Define<MethodDef>(
				LogLevel.Debug, CreateEventId(3), "Extracting constants from method: {Method}");

		internal static void LogMsgExtractingFromMethod(this ILogger logger, MethodDef methodDef) =>
			_extractingFromMethod(logger, methodDef, null);

		private static readonly Action<ILogger, int, MethodDef, Exception> _extractedFromMethod =
			LoggerMessage.Define<int, MethodDef>(
				LogLevel.Debug, CreateEventId(4), "Extracted {Count} constants from method {Method}");

		internal static void LogMsgExtractedFromMethod(this ILogger logger, MethodDef methodDef, int count) =>
			_extractedFromMethod(logger, count, methodDef, null);

		private static readonly Action<ILogger, int, ModuleDef, Exception> _extractedFromModule =
			LoggerMessage.Define<int, ModuleDef>(
				LogLevel.Information, CreateEventId(5), "Extracted {Count} constants from module {Module}");

		internal static void LogMsgExtractedFromModule(this ILogger logger, ModuleDef moduleDef, int count) =>
			_extractedFromModule(logger, count, moduleDef, null);

		private static readonly Action<ILogger, string, ModuleDef, Exception> _creatingDataBlocksStart =
			LoggerMessage.Define<string, ModuleDef>(
				LogLevel.Debug, CreateEventId(6), "Creating data block for {Size} of constants in module {Module}");
		
		internal static void LogMsgCreatingDataBlockStart(this ILogger logger, ModuleDef moduleDef, ByteSize size) =>
			_creatingDataBlocksStart(logger, size.Humanize("0.00", Culture), moduleDef, null);

		private static readonly Action<ILogger, ModuleDef, int, Exception> _creatingDataBlockFirstPassDone =
			LoggerMessage.Define<ModuleDef, int>(
				LogLevel.Trace, CreateEventId(7), "First pass of creating data block for {Module} is done. Created {Count} blocks.");
		
		internal static void LogMsgCreatingDataBlockFirstPassDone(this ILogger logger, ModuleDef moduleDef, int blockCount) =>
			_creatingDataBlockFirstPassDone(logger, moduleDef, blockCount, null);

		private static readonly Action<ILogger, ModuleDef, int, Exception> _creatingDataBlockSecondPassDone =
			LoggerMessage.Define<ModuleDef, int>(
				LogLevel.Trace, CreateEventId(8), "Second pass of creating data block for {Module} is done. {Count} remaining non-overlapping blocks.");
		
		internal static void LogMsgCreatingDataBlockSecondPassDone(this ILogger logger, ModuleDef moduleDef, int blockCount) =>
			_creatingDataBlockSecondPassDone(logger, moduleDef, blockCount, null);

		private static readonly Action<ILogger, ModuleDef, string, string, Exception> _creatingDataBlockDone =
			LoggerMessage.Define<ModuleDef, string, string>(
				LogLevel.Debug, CreateEventId(9), "Creating data block for {Module} is done. Final block has a size of {Size}. Size reduced by {Reduced}.");
		
		private static readonly Action<ILogger, ModuleDef, string, Exception> _creatingDataBlockDoneNoReduction =
			LoggerMessage.Define<ModuleDef, string>(
				LogLevel.Debug, CreateEventId(10), "Creating data block for {Module} is done. Final block has a size of {Size}.");

		internal static void LogMsgCreatingDataBlockDone(this ILogger logger, ModuleDef moduleDef, ByteSize finalSize, ByteSize maxSize) {
			var reducedSize = maxSize.Subtract(finalSize);
            if (reducedSize.Bits > 0)
				_creatingDataBlockDone(logger, moduleDef, finalSize.Humanize("0.00", Culture), reducedSize.Humanize("0.00", Culture), null);
            else
	            _creatingDataBlockDoneNoReduction(logger, moduleDef, finalSize.Humanize("0.00", Culture), null);
		}

		private static readonly Action<ILogger, ModuleDef, string, Exception> _compressDataBlockStart =
			LoggerMessage.Define<ModuleDef, string>(
				LogLevel.Debug, CreateEventId(11), "Compressing constant data block of module {Module}. Source size is {Size}");
		
		internal static void LogMsgCompressDataBlockStart(this ILogger logger, ModuleDef moduleDef, ByteSize size) =>
			_compressDataBlockStart(logger, moduleDef, size.Humanize("0.00", Culture), null);
        

		private static readonly Action<ILogger, ModuleDef, string, string, Exception> _compressDataBlockParameters =
			LoggerMessage.Define<ModuleDef, string, string>(
				LogLevel.Trace, CreateEventId(12), "Compressing constants in {ModuleDef}. Applying {Compressor} compression using mode: {Mode}");
		
		internal static void LogMsgCompressDataBlockParameters(this ILogger logger, ModuleDef moduleDef, CompressionAlgorithm algorithm, CompressionMode mode) =>
			_compressDataBlockParameters(logger, moduleDef, algorithm.Humanize(), mode.Humanize(), null);

		private static readonly Action<ILogger, ModuleDef, string, Exception> _compressDataBlockResult =
			LoggerMessage.Define<ModuleDef, string>(
				LogLevel.Trace, CreateEventId(13), "Compressing constants data block in {Module} done. Compressed size: {Size}");
		
		internal static void LogMsgCompressDataBlockResult(this ILogger logger, ModuleDef moduleDef, ByteSize size) =>
			_compressDataBlockResult(logger, moduleDef, size.Humanize("0.00", Culture), null);

		private static readonly Action<ILogger, ModuleDef, Exception> _compressDataBlockIsTooLarge =
			LoggerMessage.Define<ModuleDef>(
				LogLevel.Debug, CreateEventId(14), "Compressed constant data block in {Module} is larger than the uncompressed version. Reverting to uncompressed version.");
		
		internal static void LogMsgCompressDataBlockIsTooLarge(this ILogger logger, ModuleDef moduleDef) =>
			_compressDataBlockIsTooLarge(logger, moduleDef, null);

		private static EventId CreateEventId(int id) => new EventId(BaseId + 1, BaseStr + id.ToString("D2", CultureInfo.InvariantCulture));
	}
}
