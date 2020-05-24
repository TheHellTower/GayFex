using System;
using System.Diagnostics;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal sealed class MemberOverrideReference : INameReference<IDnlibDef> {
		readonly IMemberDef thisMemberDef;
		internal IMemberDef BaseMemberDef { get; }

		public MemberOverrideReference(IMemberDef thisMemberDef, IMemberDef baseMemberDef) {
			this.thisMemberDef = thisMemberDef ?? throw new ArgumentNullException(nameof(thisMemberDef));
			BaseMemberDef = baseMemberDef ?? throw new ArgumentNullException(nameof(baseMemberDef));
			Debug.Assert(thisMemberDef != baseMemberDef);
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			if (UTF8String.Equals(thisMemberDef.Name, BaseMemberDef.Name)) return false;
			thisMemberDef.Name = BaseMemberDef.Name;
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
