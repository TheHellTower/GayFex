using System.Text;
using System.Linq;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Confuser.Analysis;

namespace Confuser.Renamer.References {
	internal sealed class OverrideDirectiveReference : INameReference<MethodDef> {
		readonly IVTableSlot baseSlot;
		readonly IVTableSlot thisSlot;

		public bool ShouldCancelRename => baseSlot.MethodDefDeclType is GenericInstSig && thisSlot.MethodDef.Module.IsClr20;
		
		public OverrideDirectiveReference(IVTableSlot thisSlot, IVTableSlot baseSlot) {
			this.thisSlot = thisSlot;
			this.baseSlot = baseSlot;
		}

		/// <inheritdoc />
		public bool DelayRenaming(IConfuserContext context, INameService service, IDnlibDef currentDef) => false;

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
