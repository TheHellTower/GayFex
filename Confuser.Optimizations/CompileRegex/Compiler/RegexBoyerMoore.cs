using System;
using System.Diagnostics;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexBoyerMoore {
		private static readonly Type _realRegexBoyerMooreType = RU.GetRegexType("RegexBoyerMoore");

		private static readonly FieldInfo _positiveField =
			RU.GetField(_realRegexBoyerMooreType, "_positive", "Positive");

		private static readonly FieldInfo _negativeASCIIField =
			RU.GetField(_realRegexBoyerMooreType, "_negativeASCII", "NegativeASCII");

		private static readonly FieldInfo _negativeUnicodeField =
			RU.GetField(_realRegexBoyerMooreType, "_negativeUnicode", "NegativeUnicode");

		private static readonly FieldInfo _patternField = RU.GetField(_realRegexBoyerMooreType, "_pattern", "Pattern");

		private static readonly FieldInfo _lowASCIIField =
			RU.GetField(_realRegexBoyerMooreType, "_lowASCII", "LowASCII");

		private static readonly FieldInfo _highASCIIField =
			RU.GetField(_realRegexBoyerMooreType, "_highASCII", "HighASCII");

		private static readonly FieldInfo _caseInsensitiveField =
			RU.GetField(_realRegexBoyerMooreType, "_caseInsensitive", "CaseInsensitive");

		internal static RegexBoyerMoore Wrap(object realRegexPrefix) {
			if (realRegexPrefix == null) return null;

			return new RegexBoyerMoore(realRegexPrefix);
		}

		// System.Text.RegularExpressions.RegexBoyerMoore
		private object RealRegexBoyerMoore { get; }

		internal int[] Positive => (int[])_positiveField.GetValue(RealRegexBoyerMoore);
		internal int[] NegativeASCII => (int[])_negativeASCIIField.GetValue(RealRegexBoyerMoore);
		internal int[][] NegativeUnicode => (int[][])_negativeUnicodeField.GetValue(RealRegexBoyerMoore);
		internal string Pattern => (string)_patternField.GetValue(RealRegexBoyerMoore);
		internal int LowASCII => (int)_lowASCIIField.GetValue(RealRegexBoyerMoore);
		internal int HighASCII => (int)_highASCIIField.GetValue(RealRegexBoyerMoore);
		internal bool CaseInsensitive => (bool)_caseInsensitiveField.GetValue(RealRegexBoyerMoore);

		private RegexBoyerMoore(object realRegexBoyerMoore) {
			if (realRegexBoyerMoore == null) throw new ArgumentNullException(nameof(realRegexBoyerMoore));
			Debug.Assert(realRegexBoyerMoore.GetType() == _realRegexBoyerMooreType);

			RealRegexBoyerMoore = realRegexBoyerMoore;
		}
	}
}
