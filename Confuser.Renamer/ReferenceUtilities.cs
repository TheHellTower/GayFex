using System.Text;
using dnlib.DotNet;

namespace Confuser.Renamer {
	internal static class ReferenceUtilities {
		internal static StringBuilder AppendDescription(this StringBuilder builder, IDnlibDef def, INameService nameService) {
			if (nameService is null)
				return builder.AppendHashedIdentifier("Name", def.FullName);

			builder.Append("Original Name").Append(": ");
			builder.Append(nameService.GetDisplayName(def));
			builder.Append("; ");
			return builder.AppendHashedIdentifier("Name", def.FullName);
		}

		internal static StringBuilder AppendReferencedDef(this StringBuilder builder, IDnlibDef def, INameService nameService) {
			switch (def) {
				case EventDef eventDef:
					return builder.AppendReferencedEvent(eventDef, nameService);
				case FieldDef fieldDef:
					return builder.AppendReferencedField(fieldDef, nameService);
				case MethodDef methodDef:
					return builder.AppendReferencedMethod(methodDef, nameService);
				case PropertyDef propDef:
					return builder.AppendReferencedProperty(propDef, nameService);
				case TypeDef typeDef:
					return builder.AppendReferencedType(typeDef, nameService);
				default:
					return builder.Append("Referenced Definition").Append("(").AppendDescription(def, nameService).Append(")");
			}
		}

		internal static StringBuilder AppendReferencedEvent(this StringBuilder builder, EventDef eventDef, INameService nameService) =>
			builder.Append("Referenced Event").Append("(").AppendDescription(eventDef, nameService).Append(")");

		internal static StringBuilder AppendReferencedField(this StringBuilder builder, FieldDef fieldDef, INameService nameService) =>
			builder.Append("Referenced Method").Append("(").AppendDescription(fieldDef, nameService).Append(")");

		internal static StringBuilder AppendReferencedMethod(this StringBuilder builder, MethodDef methodDef, INameService nameService) =>
			builder.Append("Referenced Method").Append("(").AppendDescription(methodDef, nameService).Append(")");

		internal static StringBuilder AppendReferencedProperty(this StringBuilder builder, PropertyDef propertyDef, INameService nameService) =>
			builder.Append("Referenced Property").Append("(").AppendDescription(propertyDef, nameService).Append(")");

		internal static StringBuilder AppendReferencedType(this StringBuilder builder, TypeDef typeDef, INameService nameService) =>
			builder.Append("Referenced Type").Append("(").AppendDescription(typeDef, nameService).Append(")");

		internal static StringBuilder AppendHashedIdentifier(this StringBuilder builder, string descriptor, object value) =>
			builder.Append(descriptor).Append(" Hash: ").AppendFormat("{0:X}", value.GetHashCode());
	}
}
