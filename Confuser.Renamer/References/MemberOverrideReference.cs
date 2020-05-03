using System;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal sealed class MemberOverrideReference : INameReference<IDnlibDef> {
		readonly IMemberDef thisMemberDef;
		readonly IMemberDef baseMemberDef;

		public MemberOverrideReference(VTableSlot slot) : this(slot.MethodDef, slot.Overrides?.MethodDef) { }

		public MemberOverrideReference(IMemberDef thisMemberDef, IMemberDef baseMemberDef) {
			this.thisMemberDef = thisMemberDef ?? throw new ArgumentNullException(nameof(thisMemberDef));
			this.baseMemberDef = baseMemberDef ?? throw new ArgumentNullException(nameof(baseMemberDef));
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			if (UTF8String.Equals(thisMemberDef.Name, baseMemberDef.Name)) return false;
			thisMemberDef.Name = baseMemberDef.Name;
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
