using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Analysis {
	public static class VTableExtensions {
		public static IEnumerable<IVTableSlot> FindSlots(this IVTable vTable, IMethod method) {
			if (vTable is null) throw new ArgumentNullException(nameof(vTable));
			if (method is null) return Enumerable.Empty<IVTableSlot>();

			return vTable.AllSlots()
				.Where(slot => MethodEqualityComparer.CompareDeclaringTypes.Equals(slot.MethodDef, method));
		}

		public static IEnumerable<IVTableSlot> AllSlots(this IVTable vTable) {
			if (vTable is null) throw new ArgumentNullException(nameof(vTable));

			return vTable.Slots.Concat(vTable.InterfaceSlots.Values.SelectMany(s => s));
		}
	}
}
