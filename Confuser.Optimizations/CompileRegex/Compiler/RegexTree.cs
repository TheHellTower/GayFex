using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	using RU = ReflectionUtilities;

	[SuppressMessage("ReSharper", "StringLiteralTypo")]
	internal struct RegexTree {

		internal static readonly Type RealRegexTreeType = RU.GetRegexType("RegexTree");

		// These fields have different names and properties depending on the referenced assembly.
		// In .NET Framework and .NET Standard it's called _capnames for example and declared as internal.
		// In the .NET Core reference assembly it's called CapNames and marked public
		// (while the class is still internal)
		// We'll just search for all of the possibilities.
		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches field name RegexTree._capnames")]
		private static readonly FieldInfo _capnamesField = RU.GetField(RealRegexTreeType, "_capnames", "CapNames");
		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches field name RegexTree._capslist")]
		private static readonly FieldInfo _capslistField = RU.GetField(RealRegexTreeType, "_capslist", "CapsList");
		[SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Matches field name RegexTree._root")]
		private static readonly FieldInfo _rootField = RU.GetField(RealRegexTreeType, "_root", "Root");

		// System.Text.RegularExpressions.RegexTree
		internal object RealRegexTree { get; }

		// This may be a Hashtable or a Dictionary. Depends on the used framework. IDictionary is implemented by both.
		internal IDictionary CapNames => (IDictionary)_capnamesField.GetValue(RealRegexTree);
		internal string[] CapsList => (string[])_capslistField.GetValue(RealRegexTree);
		internal RegexNode Root => RegexNode.Wrap(_rootField.GetValue(RealRegexTree));

		internal RegexTree(object realRegexTree) {
			if (realRegexTree == null) throw new ArgumentNullException(nameof(realRegexTree));
			Debug.Assert(realRegexTree.GetType() == RealRegexTreeType);

			RealRegexTree = realRegexTree;
		}
	}
}
