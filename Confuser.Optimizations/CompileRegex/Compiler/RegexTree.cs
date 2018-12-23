using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using RU = Confuser.Optimizations.CompileRegex.Compiler.ReflectionUtilities;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal struct RegexTree {
		internal static readonly Type _realRegexTreeType = RU.GetRegexType("RegexTree");

		// These fields have different names and properties depending on the referenced assembly.
		// In .NET Framework and .NET Standard it's called _capnames for example and declared as internal.
		// In the .NET Core reference assembly it's called CapNames and marked public
		// (while the class is still internal)
		// We'll just search for all of the possibilities.
		internal static readonly FieldInfo _capnamesField = RU.GetField(_realRegexTreeType, "_capnames", "CapNames");
		internal static readonly FieldInfo _capslistField = RU.GetField(_realRegexTreeType, "_capslist", "CapsList");
		internal static readonly FieldInfo _rootField = RU.GetField(_realRegexTreeType, "_root", "Root");

		// System.Text.RegularExpressions.RegexTree
		internal object RealRegexTree { get; }

		// This may be a Hashtable or a Dictionary. Depends on the used framework. IDictionary is implemented by both.
		internal IDictionary CapNames => (IDictionary)_capnamesField.GetValue(RealRegexTree);
		internal string[] CapsList => (string[])_capslistField.GetValue(RealRegexTree);
		internal RegexNode Root => RegexNode.Wrap(_rootField.GetValue(RealRegexTree));

		internal RegexTree(object realRegexTree) {
			if (realRegexTree == null) throw new ArgumentNullException(nameof(realRegexTree));
			Debug.Assert(realRegexTree.GetType() == _realRegexTreeType);

			RealRegexTree = realRegexTree;
		}
	}
}
