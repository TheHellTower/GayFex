using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexWriter {
		private static readonly Type _realRegexWriterType;
		private static readonly MethodInfo _writeMethod;

		static RegexWriter() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexWriterType = regexAssembly.GetType("System.Text.RegularExpressions.RegexWriter", true, false);
			_writeMethod = _realRegexWriterType.GetMethod("Write",
				BindingFlags.Static | BindingFlags.NonPublic, null,
				new Type[] { RegexTree._realRegexTreeType }, null);
		}

		internal static RegexCode Write(RegexTree tree) {
			var realRegexCode = _writeMethod.Invoke(null, new object[] { tree.RealRegexTree });
			return new RegexCode(realRegexCode);
		}
	}
}
