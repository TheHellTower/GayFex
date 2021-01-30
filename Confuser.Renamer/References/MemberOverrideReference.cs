using System;
using System.Diagnostics;
using System.Text;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	public sealed class MemberOverrideReference : INameReference<IDnlibDef> {
		readonly IMemberDef thisMemberDef;
		internal IMemberDef BaseMemberDef { get; }

		public bool ShouldCancelRename => false;

		public MemberOverrideReference(IMemberDef thisMemberDef, IMemberDef baseMemberDef) {
			this.thisMemberDef = thisMemberDef ?? throw new ArgumentNullException(nameof(thisMemberDef));
			BaseMemberDef = baseMemberDef ?? throw new ArgumentNullException(nameof(baseMemberDef));
			Debug.Assert(thisMemberDef != baseMemberDef);
		}

		/// <inheritdoc />
		public bool DelayRenaming(IConfuserContext context, INameService service) => !service.IsRenamed(context, BaseMemberDef);

		public bool UpdateNameReference(IConfuserContext context, INameService service) {
			if (UTF8String.Equals(thisMemberDef.Name, BaseMemberDef.Name)) return false;
			thisMemberDef.Name = BaseMemberDef.Name;
			return true;
		}

		public override string ToString() => ToString(null, null);

		public string ToString(IConfuserContext context, INameService nameService) {
			var builder = new StringBuilder();
			builder.Append("Member Override Reference").Append("(");
			builder.Append("This ").AppendReferencedDef(thisMemberDef, context, nameService);
			builder.Append("; ");
			builder.Append("Base ").AppendReferencedDef(BaseMemberDef, context, nameService);
			builder.Append(")");
			return builder.ToString();
		}
	}
}
