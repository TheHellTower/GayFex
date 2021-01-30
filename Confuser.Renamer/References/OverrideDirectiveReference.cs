using System.Text;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal sealed class OverrideDirectiveReference : INameReference<MethodDef> {
		readonly VTableSlot baseSlot;
		readonly VTableSlot thisSlot;

		public bool ShouldCancelRename => baseSlot.MethodDefDeclType is GenericInstSig && thisSlot.MethodDef.Module.IsClr20;
		
		public OverrideDirectiveReference(VTableSlot thisSlot, VTableSlot baseSlot) {
			this.thisSlot = thisSlot;
			this.baseSlot = baseSlot;
		}

		/// <inheritdoc />
		public bool DelayRenaming(IConfuserContext context, INameService service) => false;

		public bool UpdateNameReference(IConfuserContext context, INameService service) => false;

		public override string ToString() => ToString(null, null);

		public string ToString(IConfuserContext context, INameService nameService) {
			var builder = new StringBuilder();
			builder.Append("Override directive").Append("(");
			builder.AppendReferencedMethod(thisSlot.MethodDef, context, nameService);
			builder.Append(")");
			return builder.ToString();
		}
	}
}
