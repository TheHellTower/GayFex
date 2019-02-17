using System;
using System.Reflection;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	internal static class RegexWriter {
		private static readonly Type _realRegexWriterType = RU.GetRegexType("RegexWriter");

		private static readonly MethodInfo _writeMethod =
			RU.GetStaticMethod(_realRegexWriterType, "Write", RegexTree.RealRegexTreeType);

		internal static RegexCode Write(RegexTree tree) {
			var realRegexCode = _writeMethod.Invoke(null, new[] {tree.RealRegexTree});
			return new RegexCode(realRegexCode);
		}
	}
}
