using System;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.References {
	public sealed class StringMemberNameReference : INameReference<IMemberDef> {
		private readonly Instruction _reference;
		private readonly IMemberDef _memberDef;
		public bool ShouldCancelRename => false;

		public StringMemberNameReference(Instruction reference, IMemberDef memberDef) {
			_reference = reference;
			_memberDef = memberDef;
		}

		/// <inheritdoc />
		public bool DelayRenaming(INameService service, IDnlibDef currentDef) => false;

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			switch (_reference.Operand) {
				case string strOp when string.Equals(strOp, _memberDef.Name, StringComparison.Ordinal):
				case UTF8String utf8StrOp when UTF8String.Equals(utf8StrOp, _memberDef.Name):
					return false;
				default:
					_reference.Operand = (string)_memberDef.Name;
					return true;
			}
		}

		public override string ToString() => ToString(null);

		public string ToString(INameService nameService) {
			var builder = new StringBuilder();
			builder.Append("String Member Name Reference").Append("(");

			builder.Append("Instruction").Append("(").AppendHashedIdentifier("Operand", _reference.Operand).Append(")");
			builder.Append("; ");
			builder.AppendReferencedDef(_memberDef, nameService);

			builder.Append(")");

			return builder.ToString();
		}
	}
}
