using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal struct RegexCode {
		internal static readonly Type RealRegexCodeType = RU.GetRegexType("RegexCode");

		private static readonly FieldInfo _capsField = RU.GetField(RealRegexCodeType, "_caps", "Caps");
		private static readonly FieldInfo _capsizeField = RU.GetField(RealRegexCodeType, "_capsize", "CapSize");
		private static readonly FieldInfo _trackCountField = RU.GetField(RealRegexCodeType, "_trackCount", "TrackCount");

		// System.Text.RegularExpressions.RegexCode
		internal object RealRegexCode { get; }

		// This may be a Hashtable or a Dictionary. Depends on the used framework. IDictionary is implemented by both.
		internal IDictionary Caps => (IDictionary)_capsField.GetValue(RealRegexCode);

		internal int CapSize => (int)_capsizeField.GetValue(RealRegexCode);

		internal int TrackCount => (int)_trackCountField.GetValue(RealRegexCode);

		// System.Text.RegularExpressions.RegexBoyerMoore

		internal RegexCode(object realRegexCode) {
			if (realRegexCode == null) throw new ArgumentNullException(nameof(realRegexCode));
			Debug.Assert(realRegexCode.GetType() == RealRegexCodeType);

			RealRegexCode = realRegexCode;
		}
	}
}
