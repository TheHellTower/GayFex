using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	internal class CompiledRegexRunnerFactory {
		private static readonly Type _realCompiledRegexRunnerFactoryType = RU.GetRegexType("CompiledRegexRunnerFactory");
		private static readonly FieldInfo _goMethodField = RU.GetInternalField(_realCompiledRegexRunnerFactoryType, "_goMethod", "goMethod");
		private static readonly FieldInfo _findFirstCharMethodField = RU.GetInternalField(_realCompiledRegexRunnerFactoryType, "_findFirstCharMethod", "findFirstCharMethod");
		private static readonly FieldInfo _initTrackCountMethodField = RU.GetInternalField(_realCompiledRegexRunnerFactoryType, "_initTrackCountMethod", "initTrackCountMethod");

		// System.Text.RegularExpressions.CompiledRegexRunnerFactory
		private object RealCompiledRegexRunnerFactory { get; }

		internal DynamicMethod GoMethod {
			get {
				var method = (DynamicMethod)_goMethodField.GetValue(RealCompiledRegexRunnerFactory);
				method.CreateDelegate(typeof(Action<RegexRunner>));
				return method;
			}
		}

		internal DynamicMethod FindFirstCharMethod {
			get {
				var method = (DynamicMethod)_findFirstCharMethodField.GetValue(RealCompiledRegexRunnerFactory);
				method.CreateDelegate(typeof(Func<RegexRunner, bool>));
				return method;
			}
		}

		internal DynamicMethod InitTrackCountMethod {
			get {
				var method = (DynamicMethod)_initTrackCountMethodField.GetValue(RealCompiledRegexRunnerFactory);
				method.CreateDelegate(typeof(Action<RegexRunner>));
				return method;
			}
		}

		internal CompiledRegexRunnerFactory(object realCompiledRegexRunnerFactory)  =>
			RealCompiledRegexRunnerFactory = realCompiledRegexRunnerFactory ?? throw new ArgumentNullException(nameof(realCompiledRegexRunnerFactory));
	}
}
