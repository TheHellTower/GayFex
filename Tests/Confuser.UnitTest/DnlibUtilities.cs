using System;
using System.Globalization;
using System.IO;
using dnlib.DotNet.Emit;

namespace Confuser.UnitTest {
	public static class DnlibUtilities {
		public static string WriteBody(CilBody body) {
			using (var writer = new StringWriter()) {
				WriteBody(writer, body, 0);
				return writer.ToString();
			}
		}
		public static void WriteBody(TextWriter writer, CilBody body, int indentLevel) {
			if (writer == null) throw new ArgumentNullException(nameof(writer));
			if (body == null) throw new ArgumentNullException(nameof(body));
			if (indentLevel < 0) throw new ArgumentOutOfRangeException(nameof(indentLevel), indentLevel, "The indent can't be less than 0.");

			body.UpdateInstructionOffsets();

			var culture = CultureInfo.InvariantCulture;

			var indentString = indentLevel > 0 ? new string('\t', indentLevel) : string.Empty;

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
				string OperandShortString(object operand) {
					if (operand == null) return string.Empty;
					if (operand is Instruction instr)
						return string.Format(culture, " IL_{0:X4}", instr.Offset);
					return ' ' + operand.ToString();
				}

				foreach (var instr in body.Instructions) {
					var instrLine = string.Format(CultureInfo.InvariantCulture,
						"{0}IL_{1:X4}: {2,-9}{3}",
						indentString, instr.Offset, instr.OpCode.Name,
						OperandShortString(instr.Operand));
					writer.WriteLine(instrLine.TrimEnd());
				}
			}
		}
	}
}
