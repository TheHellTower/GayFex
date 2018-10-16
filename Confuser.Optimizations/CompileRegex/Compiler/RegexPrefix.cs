using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexPrefix {
		private static readonly Type _realRegexPrefixType;
		private static readonly PropertyInfo _prefixProperty;
		private static readonly PropertyInfo _caseInsensitiveProperty;

		static RegexPrefix() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexPrefixType = regexAssembly.GetType("System.Text.RegularExpressions.RegexPrefix", true, false);

			_prefixProperty = _realRegexPrefixType.GetProperty("Prefix",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty, null,
				typeof(string), Array.Empty<Type>(), null);
			_caseInsensitiveProperty = _realRegexPrefixType.GetProperty("CaseInsensitive",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty, null,
				typeof(bool), Array.Empty<Type>(), null);
		}

		internal static RegexPrefix Wrap(object realRegexPrefix) {
			if (realRegexPrefix == null) return null;

			return new RegexPrefix(realRegexPrefix);
		}

		// System.Text.RegularExpressions.RegexPrefix
		private object RealRegexPrefix { get; }

		internal string Prefix => (string)_prefixProperty.GetGetMethod(true).Invoke(RealRegexPrefix, Array.Empty<object>());
		internal bool CaseInsensitive => (bool)_caseInsensitiveProperty.GetGetMethod(true).Invoke(RealRegexPrefix, Array.Empty<object>());

		private RegexPrefix(object realRegexPrefix) {
			if (realRegexPrefix == null) throw new ArgumentNullException(nameof(realRegexPrefix));
			Debug.Assert(realRegexPrefix.GetType() == _realRegexPrefixType);

			RealRegexPrefix = realRegexPrefix;
		}
	}
}
