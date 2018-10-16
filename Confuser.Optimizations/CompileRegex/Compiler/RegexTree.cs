using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal struct RegexTree {
		internal static readonly Type _realRegexTreeType;
		internal static readonly FieldInfo _capnamesField;
		internal static readonly FieldInfo _capslistField;

		static RegexTree() {
			var regexAssembly = typeof(Regex).Assembly;
			_realRegexTreeType = regexAssembly.GetType("System.Text.RegularExpressions.RegexTree", true, false);

			_capnamesField = _realRegexTreeType.GetField("_capnames", BindingFlags.Instance | BindingFlags.NonPublic);
			_capslistField = _realRegexTreeType.GetField("_capslist", BindingFlags.Instance | BindingFlags.NonPublic);
		}

		// System.Text.RegularExpressions.RegexTree
		internal object RealRegexTree { get; }

		// This may be a Hashtable or a Dictionary. Depends on the used framework. IDictionary is implemented by both.
		internal IDictionary _capnames => (IDictionary)_capnamesField.GetValue(RealRegexTree);
		internal string[] _capslist => (string[])_capslistField.GetValue(RealRegexTree);

		internal RegexTree(object realRegexTree) {
			if (realRegexTree == null) throw new ArgumentNullException(nameof(realRegexTree));
			Debug.Assert(realRegexTree.GetType().FullName == "System.Text.RegularExpressions.RegexTree");

			RealRegexTree = realRegexTree;
		}
	}
}
