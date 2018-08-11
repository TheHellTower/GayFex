using Confuser.Core;
using Confuser.Renamer.BAML;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLTypeReference : INameReference<TypeDef> {
		readonly TypeInfoRecord rec;
		readonly TypeSig sig;

		public BAMLTypeReference(TypeSig sig, TypeInfoRecord rec) {
			this.sig = sig;
			this.rec = rec;
		}

		public bool UpdateNameReference(IConfuserContext context, INameService service) {
			rec.TypeFullName = sig.ReflectionFullName;
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
