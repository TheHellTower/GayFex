using System.Text;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	public sealed class CAMemberReference : INameReference<IDnlibDef> {
		readonly IDnlibDef definition;
		readonly CANamedArgument namedArg;

		public bool ShouldCancelRename => false;

		public CAMemberReference(CANamedArgument namedArg, IDnlibDef definition) {
			this.namedArg = namedArg;
			this.definition = definition;
		}

		/// <inheritdoc />
		public bool DelayRenaming(IConfuserContext context, INameService service, IDnlibDef currentDef) => false;

		public bool UpdateNameReference(IConfuserContext context, INameService service) {
			if (UTF8String.Equals(namedArg.Name, definition.Name)) return false;
			namedArg.Name = definition.Name;
			return true;
		}

		public override string ToString() => ToString(null, null);

		public string ToString(IConfuserContext context, INameService nameService) {
			var builder = new StringBuilder();
			builder.Append("Custom Argument Reference").Append("(");
			builder.Append("CA Argument").Append("(").AppendHashedIdentifier("Name", namedArg.Name).Append(")");
			builder.Append("; ");
			builder.AppendReferencedDef(definition, context, nameService);
			builder.Append(")");
			return builder.ToString();
		}
	}
}
