using System.Text;
using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer {
	internal static class ReferenceUtilities {
		internal static StringBuilder AppendDescription(this StringBuilder builder, IDnlibDef def, IConfuserContext context, INameService nameService) {
			if (nameService is null || context is null)
				return builder.AppendHashedIdentifier("Name", def.FullName);
			
			builder.Append("Original Name").Append(": ");

			switch (def) {
				case TypeDef typeDef:
					builder.AppendTypeName(typeDef, context, nameService);
					break;
				case IMemberDef memberDef:
					builder.AppendTypeName(memberDef.DeclaringType, context, nameService)
						.Append("::")
						.AppendOriginalName(memberDef, context, nameService);
					break;
				default:
					builder.AppendOriginalName(def, context, nameService);
					break;
			}
			builder.Append("; ");
			return builder.AppendHashedIdentifier("Name", def.FullName);
		}

		private static StringBuilder AppendTypeName(this StringBuilder builder, TypeDef typeDef, IConfuserContext context, INameService nameService) {
			var originalNamespace = nameService.GetOriginalNamespace(context, typeDef);
			var originalName = nameService.GetOriginalName(context, typeDef);

			if (string.IsNullOrWhiteSpace(originalNamespace))
				originalNamespace = typeDef.Namespace;
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = typeDef.Name;

			if (!string.IsNullOrWhiteSpace(originalNamespace))
				builder.Append(originalNamespace).Append(".");

			return builder.Append(originalName);
		}

		private static StringBuilder AppendOriginalName(this StringBuilder builder, IDnlibDef def, IConfuserContext context, INameService nameService) {
			var originalName = nameService.GetOriginalName(context, def);
			if (string.IsNullOrWhiteSpace(originalName))
				originalName = def.Name;
			return builder.Append(originalName);
		}

		internal static StringBuilder AppendReferencedDef(this StringBuilder builder, IDnlibDef def, IConfuserContext context, INameService nameService) {
			switch (def) {
				case EventDef eventDef:
					return builder.AppendReferencedEvent(eventDef, context, nameService);
				case FieldDef fieldDef:
					return builder.AppendReferencedField(fieldDef, context, nameService);
				case MethodDef methodDef:
					return builder.AppendReferencedMethod(methodDef, context, nameService);
				case PropertyDef propDef:
					return builder.AppendReferencedProperty(propDef, context, nameService);
				case TypeDef typeDef:
					return builder.AppendReferencedType(typeDef, context, nameService);
				default:
					return builder.Append("Referenced Definition").Append("(").AppendDescription(def, context, nameService).Append(")");
			}
		}

		internal static StringBuilder AppendReferencedEvent(this StringBuilder builder, EventDef eventDef, IConfuserContext context, INameService nameService) =>
			builder.Append("Referenced Event").Append("(").AppendDescription(eventDef, context, nameService).Append(")");

		internal static StringBuilder AppendReferencedField(this StringBuilder builder, FieldDef fieldDef, IConfuserContext context, INameService nameService) =>
			builder.Append("Referenced Method").Append("(").AppendDescription(fieldDef, context, nameService).Append(")");

		internal static StringBuilder AppendReferencedMethod(this StringBuilder builder,MethodDef methodDef, IConfuserContext context, INameService nameService) =>
			builder.Append("Referenced Method").Append("(").AppendDescription(methodDef, context, nameService).Append(")");

		internal static StringBuilder AppendReferencedProperty(this StringBuilder builder, PropertyDef propertyDef, IConfuserContext context, INameService nameService) =>
			builder.Append("Referenced Property").Append("(").AppendDescription(propertyDef, context, nameService).Append(")");

		internal static StringBuilder AppendReferencedType(this StringBuilder builder, TypeDef typeDef, IConfuserContext context, INameService nameService) =>
			builder.Append("Referenced Type").Append("(").AppendDescription(typeDef, context, nameService).Append(")");

		internal static StringBuilder AppendHashedIdentifier(this StringBuilder builder, string descriptor, object value) =>
			builder.Append(descriptor).Append(" Hash: ").AppendFormat("{0:X}", value.GetHashCode());
	}
}
