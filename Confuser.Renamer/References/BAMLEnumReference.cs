using Confuser.Core;
using Confuser.Renamer.BAML;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class BAMLEnumReference : INameReference<FieldDef> {
		readonly FieldDef enumField;
		readonly PropertyRecord rec;

		public BAMLEnumReference(FieldDef enumField, PropertyRecord rec) {
			this.enumField = enumField;
			this.rec = rec;
		}

		public bool UpdateNameReference(IConfuserContext context, INameService service) {
			rec.Value = enumField.Name;
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
