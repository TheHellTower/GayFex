using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ComplexClassStructureRename.Lib;

[assembly: Obfuscation(Exclude = false, Feature = "-rename")]

namespace ComplexClassStructureRename {
	public class Program {
		[SuppressMessage("Style", "IDE0060:Remove unused parameters", Justification = "Required signature")]
		static int Main(string[] args) {
			Console.WriteLine("START");

			var t = new PublicClass2();
			t.Test();

			Console.WriteLine("END");
			return 42;
		}
	}
}
