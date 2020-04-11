using System;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Renamer.References {
	public class StringTypeReference : INameReference<TypeDef> {
		readonly Instruction reference;
		readonly TypeDef typeDef;

		public StringTypeReference(Instruction reference, TypeDef typeDef) {
			this.reference = reference;
			this.typeDef = typeDef;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			switch (reference.Operand) {
				case string strOp when string.Equals(strOp, typeDef.ReflectionFullName, StringComparison.Ordinal):
				case UTF8String utf8StrOp when UTF8String.Equals(utf8StrOp, typeDef.ReflectionFullName):
					return false;
				default:
					reference.Operand = typeDef.ReflectionFullName;
					return true;
			}
		}

		public bool ShouldCancelRename() => false;
	}
}
