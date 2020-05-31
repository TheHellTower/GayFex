using System.Text;
using dnlib.DotNet;

namespace Confuser.Renamer {
	internal static class ReferenceUtilities {
		internal static StringBuilder AppendDescription(this StringBuilder builder, IDnlibDef def, INameService nameService) {
			if (!(nameService is null)) {
				var originalNamespace = nameService.GetOriginalNamespace(def);
				var originalName = nameService.GetOriginalName(def);
				if (string.IsNullOrWhiteSpace(originalNamespace) && def is TypeDef typeDef)
					originalNamespace = typeDef.Namespace;
				if (string.IsNullOrWhiteSpace(originalName))
					originalName = def.Name;

				builder.Append("Original Name").Append(": ");
				if (!string.IsNullOrWhiteSpace(originalNamespace))
					builder.Append(originalNamespace).Append(".");
				builder.Append(originalName);
				builder.Append("; ");
			}
			return builder.AppendHashedIdentifier("Name", def.FullName);
		}

		internal static StringBuilder AppendReferencedDef(this StringBuilder builder, IDnlibDef def, INameService nameService) {
			switch (def) {
				case FieldDef fieldDef:
					return builder.AppendReferencedField(fieldDef, nameService);
				case MethodDef methodDef:
					return builder.AppendReferencedMethod(methodDef, nameService);
				case TypeDef typeDef:
					return builder.AppendReferencedType(typeDef, nameService);
				default:
					return builder.Append("Referenced Definition").Append("(").AppendDescription(def, nameService).Append(")");

			}
		}

		internal static StringBuilder AppendReferencedField(this StringBuilder builder, FieldDef fieldDef, INameService nameService) =>
			builder.Append("Referenced Method").Append("(").AppendDescription(fieldDef, nameService).Append(")");

		internal static StringBuilder AppendReferencedMethod(this StringBuilder builder, MethodDef methodDef, INameService nameService) =>
			builder.Append("Referenced Method").Append("(").AppendDescription(methodDef, nameService).Append(")");

		internal static StringBuilder AppendReferencedType(this StringBuilder builder, TypeDef typeDef, INameService nameService) =>
			builder.Append("Referenced Type").Append("(").AppendDescription(typeDef, nameService).Append(")");

		internal static StringBuilder AppendHashedIdentifier(this StringBuilder builder, string descriptor, object value) =>
			builder.Append(descriptor).Append(" Hash: ").AppendFormat("{0:X}", value.GetHashCode());
	}
}
