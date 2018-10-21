using System;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexCharClass {
		private static readonly Type _realRegexCharClassType = RU.GetRegexType("RegexCharClass");
		private static readonly MethodInfo _isSingletonMethod = 
			RU.GetStaticMethod(_realRegexCharClassType, "IsSingleton", typeof(string));
		private static readonly MethodInfo _singletonCharMethod =
			RU.GetStaticMethod(_realRegexCharClassType, "SingletonChar", typeof(string));

		internal static bool IsSingleton(string set) =>
			(bool)_isSingletonMethod.Invoke(null, new object[] { set });

		internal static char SingletonChar(string set) =>
			(char)_singletonCharMethod.Invoke(null, new object[] { set });
	}
}
