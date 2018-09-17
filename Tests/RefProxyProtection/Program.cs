using System;
using System.Collections.Generic;

namespace RefProxyProtection {
	public class Program {
		internal static int Main(string[] args) {
			var dictTest = new Dictionary<string, string> {
				{ "TestKey", "TestValue" }
			};

			Console.WriteLine("START");
			Console.WriteLine("dictTest[TestKey] = " + dictTest["TestKey"]);
			Console.WriteLine("END");
			return 42;
		}
	}
}
