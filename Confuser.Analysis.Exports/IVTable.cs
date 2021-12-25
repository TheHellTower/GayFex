using System;
using System.Collections.Generic;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Analysis {
	public interface IVTable {
		public TypeSig Type { get; }

		public IReadOnlyList<IVTableSlot> Slots { get; }
		public IReadOnlyDictionary<TypeSig, IReadOnlyList<IVTableSlot>> InterfaceSlots { get; }
	}
}
