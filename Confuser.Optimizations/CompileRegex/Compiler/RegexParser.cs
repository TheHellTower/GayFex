using System;
using System.Reflection;
using System.Text.RegularExpressions;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexParser {
		private static readonly Type _realRegexParserType = RU.GetRegexType("RegexParser");
		private static readonly MethodInfo _parseMethod = RU.GetStaticMethod(
			_realRegexParserType, "Parse", typeof(string), typeof(RegexOptions));

		internal static RegexTree Parse(string pattern, RegexOptions options) {
			var realRegexTree = _parseMethod.Invoke(null, new object[] { pattern, options });
			return new RegexTree(realRegexTree);
		}
	}
}
