using System;
using System.Diagnostics;
using Confuser.Core;

namespace Confuser.Renamer.BAML {
	internal class BAMLPropertyReference : IBAMLReference {
		PropertyRecord rec;

		public BAMLPropertyReference(PropertyRecord rec) {
			this.rec = rec;
		}

		public bool CanRename(string oldName, string newName) {
			return true;
		}

		public void Rename(string oldName, string newName) {
			var value = rec.Value;
			while (true) {
				if (value.EndsWith(oldName, StringComparison.OrdinalIgnoreCase)) {
					value = value.Substring(0, value.Length - oldName.Length) + newName;
					rec.Value = value;
				}
				else if (oldName.EndsWith(".baml", StringComparison.OrdinalIgnoreCase)) {
					oldName = ToXaml(oldName);
					newName = ToXaml(newName);
					continue;
				}

				break;
			}

			// Reaching this point means that the record was already properly replaced.
		}

		private static string ToXaml(string refName) {
			Debug.Assert(refName.EndsWith(".baml"));
			return refName.Substring(0, refName.Length - 5) + ".xaml";
		}
	}
}
