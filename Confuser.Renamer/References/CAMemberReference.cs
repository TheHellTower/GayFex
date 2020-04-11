using System;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class CAMemberReference : INameReference<IDnlibDef> {
		readonly IDnlibDef definition;
		readonly CANamedArgument namedArg;

		public CAMemberReference(CANamedArgument namedArg, IDnlibDef definition) {
			this.namedArg = namedArg;
			this.definition = definition;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			if (UTF8String.Equals(namedArg.Name, definition.Name)) return false;
			namedArg.Name = definition.Name;
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
