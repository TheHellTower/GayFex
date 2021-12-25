using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Analysis {

	internal partial class VTable {
		private sealed class VTableConstruction {
			// All virtual method slots, excluding interfaces
			public List<VTableSlot> AllSlots = new List<VTableSlot>();

			// All visible virtual method slots (i.e. excluded those being shadowed)
			public Dictionary<VTableSignature, VTableSlot> SlotsMap = new Dictionary<VTableSignature, VTableSlot>();
			public Dictionary<TypeSig, ILookup<VTableSignature, VTableSlot>> InterfaceSlots = new Dictionary<TypeSig, ILookup<VTableSignature, VTableSlot>>(TypeEqualityComparer.Instance);
		}
	}
}
