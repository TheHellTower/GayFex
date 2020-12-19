using System;
using System.Diagnostics.CodeAnalysis;

namespace EnumArrayConstantProtection {
	public class Program {
		[SuppressMessage("Style", "IDE0060:Remove unused parameters", Justification = "Required signature")]
		static int Main(string[] args) {
			Console.WriteLine("START");
			Console.WriteLine(Get(Level.A, Level.E, Level.D));
			Console.WriteLine(Get("abc", "def", "ghi"));
			Console.WriteLine("END");
			return 42;
		}

		[SuppressMessage("Style", "IDE0060:Remove unused parameters", Justification = "Just for testing.")]
		private static string Get(params Level[] levels) => "Enum Array OK";

		private static string Get(params string[] texts) => "String Array OK";
	}
}
