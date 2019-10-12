using System;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexParser {
		private static readonly Type _realRegexParserType = RU.GetRegexType("RegexParser");

		private static readonly MethodInfo _parseMethod = RU.GetStaticMethod(
			_realRegexParserType, "Parse", typeof(string), typeof(RegexOptions));
		private static readonly MethodInfo _parseMethod2 = RU.GetStaticMethod(
			_realRegexParserType, "Parse", typeof(string), typeof(RegexOptions), typeof(CultureInfo));

		// In .NET Core 3.0 the RegexWriter Class is a ref structure. We can't invoke it using reflection. For this to
		// work we'll create a dynamic method that invokes it. A dynamic method is able to invoke the function despite
		// the fact that the structure is internal.
		private static DynamicMethod _parseMethodProxy;

		private static MethodInfo GetParseMethodProxy() {
			if (_parseMethodProxy != null) return _parseMethodProxy;

			var proxyMethod = new DynamicMethod("RegexParserParseMethodProxy",
				RegexTree.RealRegexTreeType,
				new[] {typeof(string), typeof(RegexOptions), typeof(CultureInfo)},
				typeof(RegexParser).Module);

			var il = proxyMethod.GetILGenerator(256);
			
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_1);
			if (_parseMethod != null) 
				il.EmitCall(OpCodes.Call, _parseMethod, null);
			if (_parseMethod2 != null) {
				il.Emit(OpCodes.Ldarg_2);
				il.EmitCall(OpCodes.Call, _parseMethod2, null);
			}

			il.Emit(OpCodes.Ret);
			_parseMethodProxy = proxyMethod;

			return proxyMethod;
		}

		internal static RegexTree Parse(string pattern, RegexOptions options, CultureInfo culture) {
			object realRegexTree = null;
			try {
				if (_parseMethod != null)
					realRegexTree = _parseMethod.Invoke(null, new object[] {pattern, options});
				else if (_parseMethod2 != null)
					realRegexTree = _parseMethod2.Invoke(null, new object[] {pattern, options, culture});
				else
					throw new InvalidOperationException("The parse method of the regex parser couldn't be found.");
			} catch (NotSupportedException) { }

			// ReSharper disable once InvertIf
			if (realRegexTree == null) {
				var proxyMethod = GetParseMethodProxy();
				realRegexTree = proxyMethod.Invoke(null, new object[] {pattern, options, culture});
			}

			return new RegexTree(realRegexTree);
		}
	}
}
