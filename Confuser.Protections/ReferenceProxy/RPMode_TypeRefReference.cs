using Confuser.Core;
using Confuser.Renamer;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Protections.ReferenceProxy {
	internal abstract partial class RPMode {
		private sealed class TypeRefReference : INameReference<TypeDef> {
			private readonly TypeDef _typeDef;
			private readonly TypeRef _typeRef;

			public TypeRefReference(TypeRef typeRef, TypeDef typeDef) {
				_typeRef = typeRef;
				_typeDef = typeDef;
			}

			bool INameReference.UpdateNameReference(IConfuserContext context, INameService service) {
				_typeRef.Namespace = _typeDef.Namespace;
				_typeRef.Name = _typeDef.Name;
				return true;
			}

			bool INameReference.ShouldCancelRename() => false;
		}
	}
}