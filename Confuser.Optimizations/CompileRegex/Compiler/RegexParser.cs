using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Confuser.Core;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal static class RegexParser {
		private static readonly Type _realRegexParserType = RU.GetRegexType("RegexParser");

		// Spotted in .NET 4.7.2, .NET Core 2.2
		private static readonly MethodInfo _parseMethod1 = RU.GetStaticMethod(
			_realRegexParserType, "Parse", typeof(string), typeof(RegexOptions));
		
		// Spotted in NET 5.0
		private static readonly MethodInfo _parseMethod2 = RU.GetStaticMethod(
			_realRegexParserType, "Parse", typeof(string), typeof(RegexOptions), typeof(CultureInfo));

		private static DynamicMethod _generatedProxy; 

		private static DynamicMethod GenerateMethodProxy(MethodInfo parserMethodInfo) {
			var proxyMethod = new DynamicMethod(nameof(RegexParser) + parserMethodInfo.Name + "MethodProxy",
				RegexTree.RealRegexTreeType,
				parserMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray(),
				typeof(RegexParser).Module);

			var il = proxyMethod.GetILGenerator(256);
			for (var i = 0; i < parserMethodInfo.GetParameters().Length; i++) {
				il.Emit(OpCodes.Ldarg, i);
			}
			il.EmitCall(OpCodes.Call, parserMethodInfo, null);
			il.Emit(OpCodes.Ret);

			return proxyMethod;
		}

		internal static RegexTree Parse(string pattern, RegexOptions options, CultureInfo culture) {
			object realRegexTree;
			if (!(_parseMethod1 is null))
				realRegexTree = InvokeMethod(_parseMethod1, pattern, options);
			else if (!(_parseMethod2 is null))
				realRegexTree = InvokeMethod(_parseMethod2, pattern, options, culture);
			else
				throw new ConfuserException("Failed to locate System.Text.RegularExpressions.RegexParser.Parse");

			return new RegexTree(realRegexTree);
		}

		internal static object InvokeMethod(MethodInfo methodInfo, params object[] parameters) {
			// TODO: This is pretty ugly code. Can be done better once .NET Standard 2.1 is used, to use the .IsByRefLike property of the type.
			if (_generatedProxy is null)
				try {
					return methodInfo.Invoke(null, parameters);
				}
				catch (NotSupportedException) {}
			// The NotSupportedException is thrown in case the .NET Core runtime is used.
			// Reflection on RegexWriter (ref struct) does not work in this case. So we'll need a workaround.
			// We'll try to build a dynamic method to call the method we need.

			// ReSharper disable once InvertIf
			var proxyMethod = _generatedProxy;
			if (proxyMethod is null) {
				proxyMethod = GenerateMethodProxy(methodInfo);
				_generatedProxy = proxyMethod;
			}
			return proxyMethod.Invoke(null, parameters);
		}
	}
}
