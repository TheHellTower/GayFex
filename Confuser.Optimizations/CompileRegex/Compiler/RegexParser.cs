using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexParser {
		private static readonly Type _realRegexParserType;
		private static readonly MethodInfo _parseMethod;

		static RegexParser() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexParserType = regexAssembly.GetType("System.Text.RegularExpressions.RegexParser", true, false);
			_parseMethod = _realRegexParserType.GetMethod("Parse",
				BindingFlags.Static | BindingFlags.NonPublic, null,
				new Type[] { typeof(string), typeof(RegexOptions) }, null);
		}

		internal static RegexTree Parse(string pattern, RegexOptions options) {
			var realRegexTree = _parseMethod.Invoke(null, new object[] { pattern, options });
			return new RegexTree(realRegexTree);
		}
	}
}
