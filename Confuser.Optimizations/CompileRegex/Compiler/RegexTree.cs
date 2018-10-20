using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal struct RegexTree {
		internal static readonly Type _realRegexTreeType = RU.GetRegexType("RegexTree");
		internal static readonly FieldInfo _capnamesField = RU.GetInternalField(_realRegexTreeType, "_capnames");
		internal static readonly FieldInfo _capslistField = RU.GetInternalField(_realRegexTreeType, "_capslist");
		internal static readonly FieldInfo _rootField = RU.GetInternalField(_realRegexTreeType, "_root");
		// System.Text.RegularExpressions.RegexTree
		internal object RealRegexTree { get; }

		// This may be a Hashtable or a Dictionary. Depends on the used framework. IDictionary is implemented by both.
		internal IDictionary _capnames => (IDictionary)_capnamesField.GetValue(RealRegexTree);
		internal string[] _capslist => (string[])_capslistField.GetValue(RealRegexTree);
		internal RegexNode _root => RegexNode.Wrap(_rootField.GetValue(RealRegexTree));

		internal RegexTree(object realRegexTree) {
			if (realRegexTree == null) throw new ArgumentNullException(nameof(realRegexTree));
			Debug.Assert(realRegexTree.GetType() == _realRegexTreeType);

			RealRegexTree = realRegexTree;
		}
	}
}
