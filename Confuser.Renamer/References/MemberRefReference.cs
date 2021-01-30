using System.Text;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	public sealed class MemberRefReference : INameReference<IMemberDef> {
		readonly IMemberDef memberDef;
		readonly MemberRef memberRef;

		public bool ShouldCancelRename => false;

		public MemberRefReference(MemberRef memberRef, IMemberDef memberDef) {
			this.memberRef = memberRef;
			this.memberDef = memberDef;
		}

		/// <inheritdoc />
		public bool DelayRenaming(IConfuserContext context, INameService service) => false;

		public bool UpdateNameReference(IConfuserContext context, INameService service) {
			if (UTF8String.Equals(memberRef.Name, memberDef.Name)) return false;
			memberRef.Name = memberDef.Name;
			return true;
		}

		public override string ToString() => ToString(null, null); 

		public string ToString(IConfuserContext context, INameService nameService) {
			var builder = new StringBuilder();
			builder.Append("MemberRef Reference").Append("(");
			builder.Append("MemberRef").Append("(").AppendHashedIdentifier("Name", memberRef.Name).Append(")");
			builder.Append("; ");
			builder.AppendReferencedDef(memberDef, context, nameService);
			builder.Append(")");
			return builder.ToString();
		}
	}
}
