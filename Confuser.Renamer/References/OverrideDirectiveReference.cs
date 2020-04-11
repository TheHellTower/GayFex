using System;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class OverrideDirectiveReference : INameReference<MethodDef> {
		readonly VTableSlot baseSlot;
		readonly VTableSlot thisSlot;

		public OverrideDirectiveReference(VTableSlot thisSlot, VTableSlot baseSlot) {
			this.thisSlot = thisSlot;
			this.baseSlot = baseSlot;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) => false;

		public bool ShouldCancelRename() => baseSlot.MethodDefDeclType is GenericInstSig && thisSlot.MethodDef.Module.IsClr20;
	}
}
