using System;
using System.Globalization;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Renamer.References {
	internal class ResourceReference : INameReference<TypeDef> {
		readonly string format;
		readonly Resource resource;
		readonly TypeDef typeDef;

		public ResourceReference(Resource resource, TypeDef typeDef, string format) {
			this.resource = resource;
			this.typeDef = typeDef;
			this.format = format;
		}

		public bool UpdateNameReference(ConfuserContext context, INameService service) {
			var newName = string.Format(CultureInfo.InvariantCulture, format, typeDef.ReflectionFullName);
			if (UTF8String.Equals(resource.Name, newName)) return false;
			resource.Name = newName;
			return true;
		}

		public bool ShouldCancelRename() => false;
	}
}
