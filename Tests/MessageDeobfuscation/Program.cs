using System;

namespace MessageDeobfuscation {
	class Class {
		public class NestedClass {
			internal string Method(string param) {
				throw new Exception($"Exception");
				return "";
			}
		}
	}

	public class Program {
		public static int Main() {
			Console.WriteLine("START");
			try {
				new Class.NestedClass().Method("param");
			}
			catch (Exception e) {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			}
			Console.WriteLine("END");
			return 42;
		}
	}
}
