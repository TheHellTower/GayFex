using Confuser.Core;

namespace Confuser.Optimizations.CompileRegex {
	internal sealed class CompileRegexProtectionParameters {
		/// <summary>
		/// If enabled, only compile regular expressions that are explicitly marked with RegexOptions.Compiled
		/// </summary>
		internal IProtectionParameter<bool> OnlyCompiled { get; } = ProtectionParameter.Boolean("compiled", false);

		/// <summary>
		/// Expressions that use the case insensitive mode and not the culture invariant mode, may yield incorrect
		/// results for some cultures. Enableing this option will disable the compilation of those expressions.
		/// </summary>
		internal IProtectionParameter<bool> I18nSafeMode { get; } = ProtectionParameter.Boolean("i18nSafe", false);

		/// <summary>
		/// In case this option is enabled, broken expressions will be skipped. Otherwise they will cause ConfuserEx
		/// to fail obfuscating the assembly.
		/// </summary>
		internal IProtectionParameter<bool> SkipBrokenExpressions { get; } = ProtectionParameter.Boolean("skipBroken", false);
	}
}
