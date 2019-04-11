using System;
using System.Diagnostics;
using Confuser.Core;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.BAML {
	public class BAMLStringReference : IBAMLReference {
		private readonly Instruction instr;

		public BAMLStringReference(Instruction instr) => this.instr = instr;

		public bool CanRename(string oldName, string newName) => instr.OpCode.Code == Code.Ldstr;

		public void Rename(string oldName, string newName) {
			var value = (string)instr.Operand;
			while (true) {
				if (value.EndsWith(oldName, StringComparison.OrdinalIgnoreCase)) {
					value = value.Substring(0, value.Length - oldName.Length) + newName;
					instr.Operand = value;
				}
				else if (oldName.EndsWith(".baml", StringComparison.OrdinalIgnoreCase)) {
					oldName = ToXaml(oldName);
					newName = ToXaml(newName);
					continue;
				}

				break;
			}
		}

		private static string ToXaml(string refName) {
			Debug.Assert(refName.EndsWith(".baml", StringComparison.OrdinalIgnoreCase));
			return refName.Substring(0, refName.Length - 5) + ".xaml";
		}
	}
}
