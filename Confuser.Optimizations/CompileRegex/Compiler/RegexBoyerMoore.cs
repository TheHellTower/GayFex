using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexBoyerMoore {
		private static readonly Type _realRegexBoyerMooreType;

		private static readonly FieldInfo _positiveField;
		private static readonly FieldInfo _negativeASCIIField;
		private static readonly FieldInfo _negativeUnicodeField;
		private static readonly FieldInfo _patternField;
		private static readonly FieldInfo _lowASCIIField;
		private static readonly FieldInfo _highASCIIField;
		private static readonly FieldInfo _caseInsensitiveField;

		static RegexBoyerMoore() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexBoyerMooreType = regexAssembly.GetType("System.Text.RegularExpressions.RegexBoyerMoore", true, false);

			_positiveField = _realRegexBoyerMooreType.GetField("_positive",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			_negativeASCIIField = _realRegexBoyerMooreType.GetField("_negativeASCII",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			_negativeUnicodeField = _realRegexBoyerMooreType.GetField("_negativeUnicode",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			_patternField = _realRegexBoyerMooreType.GetField("_pattern",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			_lowASCIIField = _realRegexBoyerMooreType.GetField("_lowASCII",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			_highASCIIField = _realRegexBoyerMooreType.GetField("_highASCII",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
			_caseInsensitiveField = _realRegexBoyerMooreType.GetField("_caseInsensitive",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField);
		}

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
