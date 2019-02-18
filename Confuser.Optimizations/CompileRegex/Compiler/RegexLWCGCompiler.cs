using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	[SuppressMessage("ReSharper", "StringLiteralTypo", Justification = "Lots of references to fields here.")]
	[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Lots of references to fields here.")]
	[SuppressMessage("ReSharper", "CommentTypo", Justification = "Lots of references to fields here.")]
	internal sealed class RegexLWCGCompiler {
		private static readonly Type _realRegexLwcgCompilerType = RU.GetRegexType("RegexLWCGCompiler");

		private static readonly ConstructorInfo _constructor = RU.GetInstanceConstructor(_realRegexLwcgCompilerType);

		private static readonly MethodInfo _factoryInstanceFromCodeMethod = RU.GetMethod(
			_realRegexLwcgCompilerType, "FactoryInstanceFromCode", RegexCode.RealRegexCodeType, typeof(RegexOptions));

		// System.Text.RegularExpressions.RegexLWCGCompiler
		private object RealRegexLWCGCompiler { get; }

		internal RegexLWCGCompiler() :
			this(_constructor.Invoke(Array.Empty<object>())) {
		}

		private RegexLWCGCompiler(object realRegexLwcgCompiler) =>
			RealRegexLWCGCompiler =
				realRegexLwcgCompiler ?? throw new ArgumentNullException(nameof(realRegexLwcgCompiler));

		internal CompiledRegexRunnerFactory FactoryInstanceFromCode(RegexCode code, RegexOptions options) =>
			new CompiledRegexRunnerFactory(_factoryInstanceFromCodeMethod.Invoke(RealRegexLWCGCompiler, new[] {code.RealRegexCode, options}));
	}
}
