using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexNode : IEquatable<RegexNode> {
		internal static readonly Type _realRegexNodeType;
		private static readonly FieldInfo _childrenField;
		private static readonly FieldInfo _nextField;
		private static readonly FieldInfo _optionsField;

		static RegexNode() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexNodeType = regexAssembly.GetType("System.Text.RegularExpressions.RegexNode", true, false);

			_childrenField = _realRegexNodeType.GetField("_children", BindingFlags.NonPublic | BindingFlags.Instance);
			_nextField = _realRegexNodeType.GetField("_next", BindingFlags.NonPublic | BindingFlags.Instance);
			_optionsField = _realRegexNodeType.GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		internal static RegexNode Wrap(object realRegexNode) {
			if (realRegexNode == null) return null;

			return new RegexNode(realRegexNode);
		}

		// System.Text.RegularExpressions.RegexNode
		private object RealRegexNode { get; }

		internal List<RegexNode> _children {
			get {
				var fieldValue = _childrenField.GetValue(RealRegexNode);
				if (fieldValue == null) return null;

				if (!(fieldValue is IList childrenList))
					throw new InvalidOperationException("Illegal type in _children field.");

				return childrenList.Cast<object>().Select(Wrap).ToList();
			}
		}

		internal RegexNode _next => Wrap(_nextField.GetValue(RealRegexNode));
		internal RegexOptions _options => (RegexOptions)_optionsField.GetValue(RealRegexNode);

		private RegexNode(object realRegexNode) {
			if (realRegexNode == null) throw new ArgumentNullException(nameof(realRegexNode));
			Debug.Assert(realRegexNode.GetType() == _realRegexNodeType);

			RealRegexNode = realRegexNode;
		}

		public override bool Equals(object obj) => Equals(obj as RegexNode);

		public bool Equals(RegexNode other) => other != null && RealRegexNode.Equals(other.RealRegexNode);

		public override int GetHashCode() => RealRegexNode.GetHashCode();
	}
}
