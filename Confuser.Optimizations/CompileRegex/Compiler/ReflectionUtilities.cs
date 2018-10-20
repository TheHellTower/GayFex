using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class ReflectionUtilities {
		internal static Type GetRegexType(string name) {
			var regexAssembly = typeof(Regex).Assembly;
			return regexAssembly.GetType("System.Text.RegularExpressions." + name, true, false);
		}

		internal static FieldInfo GetInternalField(Type declaringType, string name) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));

			return declaringType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
		}

		internal static MethodInfo GetStaticInternalMethod(Type declaringType, string name, params Type[] parameters) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			return declaringType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null, parameters, null);
		}

		internal static PropertyInfo GetInternalProperty(Type declaringType, string name, Type returnType) {
			if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
			if (returnType == null) throw new ArgumentNullException(nameof(returnType));

			return declaringType.GetProperty(name,
				BindingFlags.Instance | BindingFlags.NonPublic, null,
				returnType, Array.Empty<Type>(), null);
		}
	}
}
