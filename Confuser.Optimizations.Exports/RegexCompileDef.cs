using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations {
	public struct RegexCompileDef : IEquatable<RegexCompileDef> {
		public string Pattern { get; }
		public RegexOptions Options { get; }
		public TimeSpan? Timeout { get; }
		public bool StaticTimeout { get; }
		public CultureInfo Culture => CultureInfo.InvariantCulture;
		public ISet<IRegexTargetMethod> TargetMethods { get; }

		public RegexCompileDef(string pattern, RegexOptions options, TimeSpan? timeout, bool staticTimeout) {
			Pattern = pattern;
			Options = options;
			Timeout = timeout;
			StaticTimeout = staticTimeout;
			TargetMethods = null;
		}

		public RegexCompileDef(string pattern, RegexOptions options, TimeSpan? timeout, bool staticTimeout,
			ISet<IRegexTargetMethod> targetMethods) {
			Pattern = pattern;
			Options = options;
			Timeout = timeout;
			StaticTimeout = staticTimeout;
			TargetMethods = targetMethods;
		}

		public override bool Equals(object obj) =>
			(obj is RegexCompileDef) ? base.Equals((RegexCompileDef)obj) : false;

		public bool Equals(RegexCompileDef other) =>
			Pattern == other.Pattern && Options == other.Options && Nullable.Equals(Timeout, other.Timeout) &&
			StaticTimeout == other.StaticTimeout;

		public override int GetHashCode() =>
			(Pattern, Options, Timeout, StaticTimeout).GetHashCode();

		public static bool operator ==(RegexCompileDef def1, RegexCompileDef def2) => def1.Equals(def2);
		public static bool operator !=(RegexCompileDef def1, RegexCompileDef def2) => !def1.Equals(def2);
	}
}
