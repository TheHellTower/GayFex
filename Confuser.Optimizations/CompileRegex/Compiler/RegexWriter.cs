using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	internal static class RegexWriter {
		private static readonly Type _realRegexWriterType = RU.GetRegexType("RegexWriter");

		private static readonly MethodInfo _writeMethod =
			RU.GetStaticMethod(_realRegexWriterType, "Write", RegexTree.RealRegexTreeType);

#if !NETFRAMEWORK
		// In .NET Core the RegexWriter Class is a ref structure. We can't invoke it using reflection. For this to
		// work we'll create a dynamic method that invokes it. A dynamic method is able to invoke the function despite
		// the fact that the structure is internal.
		private static DynamicMethod _writeMethodProxy;

		private static DynamicMethod GetWriteMethodProxy() {
			if (_writeMethodProxy != null) return _writeMethodProxy;

			var proxyMethod = new DynamicMethod("RegexWriterWriteMethodProxy",
				RegexCode.RealRegexCodeType,
				new[] { RegexTree.RealRegexTreeType },
				typeof(RegexWriter).Module);

			var il = proxyMethod.GetILGenerator(256);
			il.Emit(OpCodes.Ldarg_0);
			il.EmitCall(OpCodes.Call, _writeMethod, null);
			il.Emit(OpCodes.Ret);
			_writeMethodProxy = proxyMethod;

			return proxyMethod;
		}
#endif

		internal static RegexCode Write(RegexTree tree) {
#if NETFRAMEWORK
			var writeMethod = _writeMethod;
#else
			var writeMethod = GetWriteMethodProxy();
#endif

			var realRegexCode = writeMethod.Invoke(null, new[] { tree.RealRegexTree });
			return new RegexCode(realRegexCode);
		}
	}
}
