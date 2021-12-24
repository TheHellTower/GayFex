using System;
using System.Text;
using Confuser.Core;
using Confuser.Renamer.BAML;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal sealed class BAMLTypeReference : INameReference<TypeDef> {
		readonly TypeInfoRecord rec;
		readonly TypeSig sig;

		public bool ShouldCancelRename => false;

		public BAMLTypeReference(TypeSig sig, TypeInfoRecord rec) {
			this.sig = sig;
			this.rec = rec;
		}

		public bool UpdateNameReference(IConfuserContext context, INameService service) {
			if (string.Equals(rec.TypeFullName, sig.ReflectionFullName, StringComparison.Ordinal)) return false;
			rec.TypeFullName = sig.ReflectionFullName;
			return true;
		}

		/// <inheritdoc />
		public bool DelayRenaming(IConfuserContext context, INameService service, IDnlibDef currentDef) => false;

		public override string ToString() => ToString(null, null);

		public string ToString(IConfuserContext context, INameService nameService) {
			var builder = new StringBuilder();
			builder.Append("BAML Type Reference").Append("(");
			builder.Append("Type Info Record").Append("(").AppendHashedIdentifier("Name", rec.TypeFullName).Append(")");
			builder.Append("; ");
			builder.Append("Type Signature").Append("(").AppendHashedIdentifier("Name", sig.ReflectionFullName).Append(")");
			builder.Append(")");
			return builder.ToString();
		}
	}
}
