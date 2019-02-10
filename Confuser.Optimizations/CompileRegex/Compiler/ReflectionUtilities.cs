using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class ReflectionUtilities {
		internal static Type GetRegexType(string name) {
			var regexAssembly = typeof(Regex).Assembly;
			var fullName = "System.Text.RegularExpressions." + name;
			var resultType = regexAssembly.GetType("System.Text.RegularExpressions." + name, true, false);
			Debug.Assert(resultType != null, $"Failed to find type {fullName}");
			return resultType;
		}

		internal static FieldInfo GetField(Type declaringType, string name) =>
			GetField(declaringType, name, Array.Empty<string>());

		internal static FieldInfo GetField(Type declaringType, string name, params string[] altNames) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));

			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

			var resultField = declaringType.GetField(name, flags);
			if (resultField == null) {
				foreach (var altName in altNames) {
					resultField = declaringType.GetField(altName, flags);
					if (resultField != null) break;
				}
			}

			Debug.Assert(resultField != null, $"Failed to find field {name} in type {declaringType.FullName}");
			return resultField;
		}

		internal static FieldInfo GetInternalField(Type declaringType, string name) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));

			var resultField = declaringType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
			Debug.Assert(resultField != null, $"Failed to find field {name} in type {declaringType.FullName}");
			return resultField;
		}

		internal static MethodInfo GetStaticMethod(Type declaringType, string name, params Type[] parameters) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var resultMethod = declaringType.GetMethod(name,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
			Debug.Assert(resultMethod != null, $"Failed to find method {name} in type {declaringType.FullName}");
			return resultMethod;
		}

		internal static MethodInfo GetStaticInternalMethod(Type declaringType, string name, params Type[] parameters) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var resultMethod = declaringType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null,
				parameters, null);
			Debug.Assert(resultMethod != null, $"Failed to find method {name} in type {declaringType.FullName}");
			return resultMethod;
		}

		internal static PropertyInfo GetInternalProperty(Type declaringType, string name, Type returnType) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
			if (returnType == null) throw new ArgumentNullException(nameof(returnType));

			var resultProperty = declaringType.GetProperty(name,
				BindingFlags.Instance | BindingFlags.NonPublic, null,
				returnType, Array.Empty<Type>(), null);
			Debug.Assert(resultProperty != null, $"Failed to find property {name} in type {declaringType.FullName}");
			return resultProperty;
		}
	}
}
