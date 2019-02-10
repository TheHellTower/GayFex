using System;
using System.Diagnostics;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexPrefix {
		private static readonly Type _realRegexPrefixType = RU.GetRegexType("RegexPrefix");

		private static readonly PropertyInfo _prefixProperty =
			RU.GetInternalProperty(_realRegexPrefixType, "Prefix", typeof(string));

		private static readonly PropertyInfo _caseInsensitiveProperty =
			RU.GetInternalProperty(_realRegexPrefixType, "CaseInsensitive", typeof(bool));

		internal static RegexPrefix Wrap(object realRegexPrefix) {
			if (realRegexPrefix == null) return null;

			return new RegexPrefix(realRegexPrefix);
		}

		// System.Text.RegularExpressions.RegexPrefix
		private object RealRegexPrefix { get; }

		internal string Prefix =>
			(string)_prefixProperty.GetGetMethod(true).Invoke(RealRegexPrefix, Array.Empty<object>());

		internal bool CaseInsensitive =>
			(bool)_caseInsensitiveProperty.GetGetMethod(true).Invoke(RealRegexPrefix, Array.Empty<object>());

		private RegexPrefix(object realRegexPrefix) {
			if (realRegexPrefix == null) throw new ArgumentNullException(nameof(realRegexPrefix));
			Debug.Assert(realRegexPrefix.GetType() == _realRegexPrefixType);

			RealRegexPrefix = realRegexPrefix;
		}
	}
}
