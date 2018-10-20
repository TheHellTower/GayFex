using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexNode : IEquatable<RegexNode> {
		internal static readonly Type _realRegexNodeType = RU.GetRegexType("RegexNode");
		private static readonly FieldInfo _childrenField = RU.GetInternalField(_realRegexNodeType, "_children");
		private static readonly FieldInfo _nextField = RU.GetInternalField(_realRegexNodeType, "_next");
		private static readonly FieldInfo _optionsField = RU.GetInternalField(_realRegexNodeType, "_options");

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
