using System;

namespace ReferenceProxy {
	class Program {
		static int Main(string[] args) {
			Console.WriteLine("START");
			foreach (var arg in args)
				Console.WriteLine(arg);
			Console.WriteLine("END");
			return 42;
		}
	}
}
