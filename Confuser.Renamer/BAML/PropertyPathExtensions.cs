using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Renamer.BAML {
	internal static class PropertyPathExtensions {
		internal static string GetTypeName(this SourceValueInfo info) {
			var propertyName = info.name?.Trim();
			if (propertyName != null && propertyName.StartsWith("(") && propertyName.EndsWith(")")) {
				var indexOfDot = propertyName.LastIndexOf(".");
				if (indexOfDot < 0) return null;
				return propertyName.Substring(1, indexOfDot - 1);
			}
			return null;
		}

		internal static string GetPropertyName(this SourceValueInfo info) {
			switch (info.type) {
				case SourceValueType.Direct:
					return null;
				case SourceValueType.Property:
					var propertyName = info.name?.Trim();
					if (propertyName != null && propertyName.StartsWith("(") && propertyName.EndsWith(")")) {
						var indexOfDot = propertyName.LastIndexOf(".");
						if (indexOfDot < 0) return propertyName.Substring(1, propertyName.Length - 2);
						return propertyName.Substring(indexOfDot + 1, propertyName.Length - indexOfDot - 2);
					}
					return propertyName;
				case SourceValueType.Indexer:
					return "Item";
				default:
					throw new InvalidOperationException("Unexpected SourceValueType.");
			}
		}
	}
}
