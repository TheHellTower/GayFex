using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexCharClass {
		private static readonly Type _realRegexCharClassType;
		private static readonly MethodInfo _isSingletonMethod;
		private static readonly MethodInfo _singletonCharMethod;

		static RegexCharClass() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexCharClassType = regexAssembly.GetType("System.Text.RegularExpressions.RegexCharClass", true, false);

			_isSingletonMethod = _realRegexCharClassType.GetMethod("IsSingleton",
				BindingFlags.Static | BindingFlags.NonPublic, null,
				new Type[] { typeof(string) }, null);
			_singletonCharMethod = _realRegexCharClassType.GetMethod("SingletonChar",
				BindingFlags.Static | BindingFlags.NonPublic, null,
				new Type[] { typeof(string) }, null);
		}

		internal static bool IsSingleton(string set) =>
			(bool)_isSingletonMethod.Invoke(null, new object[] { set });

		internal static char SingletonChar(string set) =>
			(char)_singletonCharMethod.Invoke(null, new object[] { set });
	}
}
