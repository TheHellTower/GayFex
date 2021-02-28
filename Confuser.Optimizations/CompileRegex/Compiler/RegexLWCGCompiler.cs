using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Confuser.Core;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	[SuppressMessage("ReSharper", "StringLiteralTypo", Justification = "Lots of references to fields here.")]
	[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Lots of references to fields here.")]
	[SuppressMessage("ReSharper", "CommentTypo", Justification = "Lots of references to fields here.")]
	internal sealed class RegexLWCGCompiler {
		private static readonly Type _realRegexLwcgCompilerType = RU.GetRegexType("RegexLWCGCompiler");

		private static readonly ConstructorInfo _constructor = RU.GetInstanceConstructor(_realRegexLwcgCompilerType);
		
		// Spotted in .NET 4.7.2, .NET Core 2.2
		private static readonly MethodInfo _factoryInstanceFromCodeMethod1 = RU.GetMethod(
			_realRegexLwcgCompilerType, "FactoryInstanceFromCode", RegexCode.RealRegexCodeType, typeof(RegexOptions));

		// Spotted in NET 5.0
		private static readonly MethodInfo _factoryInstanceFromCodeMethod2 = RU.GetMethod(
			_realRegexLwcgCompilerType, "FactoryInstanceFromCode", typeof(string), RegexCode.RealRegexCodeType, typeof(RegexOptions), typeof(bool));

		// System.Text.RegularExpressions.RegexLWCGCompiler
		private object RealRegexLWCGCompiler { get; }

		internal RegexLWCGCompiler() :
			this(_constructor.Invoke(Array.Empty<object>())) {
		}

		private RegexLWCGCompiler(object realRegexLwcgCompiler) =>
			RealRegexLWCGCompiler =
				realRegexLwcgCompiler ?? throw new ArgumentNullException(nameof(realRegexLwcgCompiler));

		internal CompiledRegexRunnerFactory FactoryInstanceFromCode(string pattern, RegexCode code, RegexOptions options, bool hasTimeout) {
			object runnerFactory;
			if (!(_factoryInstanceFromCodeMethod1 is null))
				runnerFactory = _factoryInstanceFromCodeMethod1.Invoke(RealRegexLWCGCompiler, new[] {code.RealRegexCode, options});
			else if (!(_factoryInstanceFromCodeMethod2 is null))
				runnerFactory = _factoryInstanceFromCodeMethod2.Invoke(RealRegexLWCGCompiler, new[] {pattern, code.RealRegexCode, options, hasTimeout});
			else
				throw new ConfuserException("Failed to locate System.Text.RegularExpressions.RegexLWCGCompiler.FactoryInstanceFromCode");

			return new CompiledRegexRunnerFactory(runnerFactory);
		}
	}
}
