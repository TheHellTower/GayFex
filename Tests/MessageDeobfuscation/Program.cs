using System;

namespace MessageDeobfuscation {
	class Class {
		public string Method(string param1, int param2) => "method";

		public string Field = "field";

		public string Property => "property";

		public event EventHandler<string> Event;

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
			catch (Exception ex) {
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
			}
			Console.WriteLine("END");
			return 42;
		}
	}
}
