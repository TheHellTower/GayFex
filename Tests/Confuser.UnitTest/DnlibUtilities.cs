using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Confuser.UnitTest.Properties;
using dnlib.DotNet.Emit;

namespace Confuser.UnitTest {
	public static class DnlibUtilities {
		private static CultureInfo Culture => CultureInfo.InvariantCulture;

		public static string WriteBody(CilBody body) {
			using (var writer = new StringWriter()) {
				WriteBody(writer, body, 0);
				return writer.ToString();
			}
		}

		public static void WriteBody(TextWriter writer, CilBody body, int indentLevel) {
			if (writer == null) throw new ArgumentNullException(nameof(writer));
			if (body == null) throw new ArgumentNullException(nameof(body));
			if (indentLevel < 0) throw new ArgumentOutOfRangeException(nameof(indentLevel), indentLevel, Resources.OutOfRange_IndentLevel);

			body.UpdateInstructionOffsets();
			var indentString = GetIndentString(indentLevel);

			writer.WriteLine("{0}// Header Size: {1:d} bytes", indentString, body.HeaderSize);
			writer.WriteLine("{0}.maxstack {1:d}", indentString, body.MaxStack);
			if (body.HasVariables) {
				writer.WriteLine("{0}.locals {1}(", indentString, body.InitLocals ? "init " : string.Empty);
				foreach (var local in body.Variables) {
					var name = local.Name;
					if (!string.IsNullOrEmpty(name)) name = ' ' + name;
					writer.WriteLine("{0}\t[{1:d}] {2}{3},", indentString, local.Index, local.Type, name);
				}
				writer.WriteLine("{0})", indentString);
			}

			if (body.HasInstructions) {
				writer.WriteLine();

				var remainingHandlers = body.ExceptionHandlers.ToList();
				var currentIndent = indentLevel;

				var currentExceptionBlocks = new Stack<(ExceptionHandler Handler, HandlerPart Part)>();

				var currentIndex = 0;
				while (currentIndex < body.Instructions.Count) {
					var nextHandlerStart = remainingHandlers
						.Select(h => body.Instructions.IndexOf(h.TryStart))
						.Where(i => i >= currentIndex)
						.DefaultIfEmpty(body.Instructions.Count)
						.Min();
					if (nextHandlerStart < currentIndex) nextHandlerStart = body.Instructions.Count;

					var currentHandlerEnd = body.Instructions.Count;
					if (currentExceptionBlocks.Count > 0) {
						var exHandlerAndPart = currentExceptionBlocks.Peek();
						switch (exHandlerAndPart.Part) {
							case HandlerPart.Try:
								if (exHandlerAndPart.Handler.TryEnd != null)
									currentHandlerEnd = body.Instructions.IndexOf(exHandlerAndPart.Handler.TryEnd);
								break;
							case HandlerPart.Filter:
								if (exHandlerAndPart.Handler.HandlerStart != null)
									currentHandlerEnd = body.Instructions.IndexOf(exHandlerAndPart.Handler.HandlerStart);
								break;
							case HandlerPart.Handler:
								if (exHandlerAndPart.Handler.HandlerEnd != null)
									currentHandlerEnd = body.Instructions.IndexOf(exHandlerAndPart.Handler.HandlerEnd);
								break;
						}
					}

					var nextStateChange = Math.Min(nextHandlerStart, currentHandlerEnd);

					if (nextStateChange != currentIndex) {
						WriteInstructions(writer, body.Instructions.Range(currentIndex, nextStateChange), currentIndent);
						currentIndex = nextStateChange;
						continue;
					}

					Debug.Assert(currentIndex == nextStateChange && nextStateChange >= 0);

					var currentInstruction = body.Instructions[currentIndex];
					if (currentIndex == currentHandlerEnd) {
						var (currentHandler, currentHandlerPart) = currentExceptionBlocks.Pop();
						if (currentHandlerPart == HandlerPart.Try && currentHandler.FilterStart == currentInstruction) {
							writer.WriteLine(GetIndentString(currentIndent - 1) + "} .filter {");
							currentHandlerPart = HandlerPart.Filter;
						}
						if (currentHandlerPart != HandlerPart.Handler && currentHandler.HandlerStart == currentInstruction) {
							switch (currentHandler.HandlerType) {
								case ExceptionHandlerType.Catch:
									writer.WriteLine(GetIndentString(currentIndent - 1) + "} catch (" + currentHandler.CatchType.FullName + ") {");
									break;
								case ExceptionHandlerType.Filter:
									writer.WriteLine(GetIndentString(currentIndent - 1) + "} .filter {");
									break;
								case ExceptionHandlerType.Finally:
									writer.WriteLine(GetIndentString(currentIndent - 1) + "} finally {");
									break;
								case ExceptionHandlerType.Fault:
									writer.WriteLine(GetIndentString(currentIndent - 1) + "} .fault {");
									break;
								case ExceptionHandlerType.Duplicated:
									writer.WriteLine(GetIndentString(currentIndent - 1) + "} .duplicated {");
									break;
							}
							currentHandlerPart = HandlerPart.Handler;
						}
						if ((currentHandlerPart == HandlerPart.Try && currentHandler.TryEnd == currentInstruction)
							|| (currentHandlerPart == HandlerPart.Handler && currentHandler.HandlerEnd == currentInstruction)) {
							currentIndent--;
							writer.WriteLine(GetIndentString(currentIndent) + "}");
						}
						else {
							currentExceptionBlocks.Push((currentHandler, currentHandlerPart));
						}

						continue;
					}

					if (currentIndex == nextHandlerStart) {
						var matchingHandlers = remainingHandlers.Where(h => h.TryStart == currentInstruction).ToList();
						if (matchingHandlers.Count == 0) throw new InvalidOperationException(Resources.InvalidOperation_FaultyExHandlers);

						ExceptionHandler currentHandler;
						if (matchingHandlers.Count == 1) currentHandler = matchingHandlers[0];
						else {
							int GetBlockEndIndex(ExceptionHandler handler) {
								var result = body.Instructions.Count - 1;
								if (handler.FilterStart != null)
									result = Math.Min(body.Instructions.IndexOf(handler.FilterStart), result);

								if (handler.HandlerStart != null)
									result = Math.Min(body.Instructions.IndexOf(handler.HandlerStart), result);

								if (handler.TryEnd != null)
									result = Math.Min(body.Instructions.IndexOf(handler.TryEnd), result);

								return result;
							}
							currentHandler = matchingHandlers.OrderBy(GetBlockEndIndex).First();
						}

						writer.WriteLine(GetIndentString(currentIndent) + "try {");
						currentIndent++;
						remainingHandlers.Remove(currentHandler);
						currentExceptionBlocks.Push((currentHandler, HandlerPart.Try));
					}
				}
			}
		}

		private static string GetIndentString(int indentLevel) =>
			indentLevel > 0 ? new string('\t', indentLevel) : string.Empty;

		private static void WriteInstructions(TextWriter writer, IEnumerable<Instruction> instructions, int indentLevel) {
			var indentString = indentLevel > 0 ? new string('\t', indentLevel) : string.Empty;

			string OperandShortString(object operand) {
				if (operand == null) return string.Empty;
				if (operand is Instruction instr)
					return string.Format(Culture, " IL_{0:X4}", instr.Offset);
				return ' ' + operand.ToString();
			}

			foreach (var instr in instructions) {
				var instrLine = string.Format(CultureInfo.InvariantCulture,
					"{0}IL_{1:X4}: {2,-9}{3}",
					indentString, instr.Offset, instr.OpCode.Name,
					OperandShortString(instr.Operand));
				writer.WriteLine(instrLine.TrimEnd());
			}
		}

		private static IEnumerable<T> Range<T>(this IList<T> list, int firstIndexInclusive, int lastIndexExclusive) {
			for (var i = firstIndexInclusive; i < lastIndexExclusive; i++) {
				yield return list[i];
			}
		}

		private enum HandlerPart {
			@Try,
			Filter,
			Handler,
		}
	}
}
