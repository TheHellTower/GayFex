using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	internal static class RegexWriter {
		private static readonly Type _realRegexWriterType = RU.GetRegexType("RegexWriter");

		private static readonly MethodInfo _writeMethod =
			RU.GetStaticMethodThrow(_realRegexWriterType, "Write", RegexTree.RealRegexTreeType);

		// In .NET Core the RegexWriter Class is a ref structure. We can't invoke it using reflection. For this to
		// work we'll create a dynamic method that invokes it. A dynamic method is able to invoke the function despite
		// the fact that the structure is internal.
		private static DynamicMethod _writeMethodProxy;

		private static MethodInfo GetWriteMethodProxy() {
			if (_writeMethodProxy != null) return _writeMethodProxy;

			var proxyMethod = new DynamicMethod("RegexWriterWriteMethodProxy",
				RegexCode.RealRegexCodeType,
				new[] {RegexTree.RealRegexTreeType},
				typeof(RegexWriter).Module);

			var il = proxyMethod.GetILGenerator(256);
			il.Emit(OpCodes.Ldarg_0);
			il.EmitCall(OpCodes.Call, _writeMethod, null);
			il.Emit(OpCodes.Ret);
			_writeMethodProxy = proxyMethod;

			return proxyMethod;
		}

		internal static RegexCode Write(RegexTree tree) {
			// TODO: This is pretty ugly code. Can be done better once .NET Standard 2.1 is used, to use the .IsByRefLike property of the type.
			object realRegexCode = null;
			if (_writeMethodProxy is null)
				try {
					realRegexCode = _writeMethod.Invoke(null, new[] {tree.RealRegexTree});
				}
				catch (NotSupportedException) {}
			// The NotSupportedException is thrown in case the .NET Core runtime is used.
			// Reflection on RegexWriter (ref struct) does not work in this case. So we'll need a workaround.
			// We'll try to build a dynamic method to call the method we need.

			// ReSharper disable once InvertIf
			if (realRegexCode == null) {
				var writeMethod = GetWriteMethodProxy();
				realRegexCode = writeMethod.Invoke(null, new[] { tree.RealRegexTree });
			}

			return new RegexCode(realRegexCode);
		}
	}
}
