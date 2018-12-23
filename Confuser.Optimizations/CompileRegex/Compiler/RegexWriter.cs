using System;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexWriter {
		private static readonly Type _realRegexWriterType = RU.GetRegexType("RegexWriter");
		private static readonly MethodInfo _writeMethod = 
			RU.GetStaticMethod(_realRegexWriterType, "Write", RegexTree._realRegexTreeType);

		internal static RegexCode Write(RegexTree tree) {
			var realRegexCode = _writeMethod.Invoke(null, new object[] { tree.RealRegexTree });
			return new RegexCode(realRegexCode);
		}
	}
}
