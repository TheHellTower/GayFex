using System;
using System.Diagnostics;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexBoyerMoore {
		private static readonly Type _realRegexBoyerMooreType = RU.GetRegexType("RegexBoyerMoore");

		private static readonly FieldInfo _positiveField = RU.GetInternalField(_realRegexBoyerMooreType, "_positive");
		private static readonly FieldInfo _negativeASCIIField = RU.GetInternalField(_realRegexBoyerMooreType, "_negativeASCII");
		private static readonly FieldInfo _negativeUnicodeField = RU.GetInternalField(_realRegexBoyerMooreType, "_negativeUnicode");
		private static readonly FieldInfo _patternField = RU.GetInternalField(_realRegexBoyerMooreType, "_pattern");
		private static readonly FieldInfo _lowASCIIField = RU.GetInternalField(_realRegexBoyerMooreType, "_lowASCII");
		private static readonly FieldInfo _highASCIIField = RU.GetInternalField(_realRegexBoyerMooreType, "_highASCII");
		private static readonly FieldInfo _caseInsensitiveField = RU.GetInternalField(_realRegexBoyerMooreType, "_caseInsensitive");

		internal static RegexBoyerMoore Wrap(object realRegexPrefix) {
			if (realRegexPrefix == null) return null;

			return new RegexBoyerMoore(realRegexPrefix);
		}

		// System.Text.RegularExpressions.RegexBoyerMoore
		private object RealRegexBoyerMoore { get; }

		internal int[] _positive => (int[])_positiveField.GetValue(RealRegexBoyerMoore);
		internal int[] _negativeASCII => (int[])_negativeASCIIField.GetValue(RealRegexBoyerMoore);
		internal int[][] _negativeUnicode => (int[][])_negativeUnicodeField.GetValue(RealRegexBoyerMoore);
		internal string _pattern => (string)_patternField.GetValue(RealRegexBoyerMoore);
		internal int _lowASCII => (int)_lowASCIIField.GetValue(RealRegexBoyerMoore);
		internal int _highASCII => (int)_highASCIIField.GetValue(RealRegexBoyerMoore);
		internal bool _caseInsensitive => (bool)_caseInsensitiveField.GetValue(RealRegexBoyerMoore);

		private RegexBoyerMoore(object realRegexBoyerMoore) {
			if (realRegexBoyerMoore == null) throw new ArgumentNullException(nameof(realRegexBoyerMoore));
			Debug.Assert(realRegexBoyerMoore.GetType() == _realRegexBoyerMooreType);

			RealRegexBoyerMoore = realRegexBoyerMoore;
		}
	}
}
