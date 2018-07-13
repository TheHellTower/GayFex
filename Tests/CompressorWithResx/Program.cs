using System;

namespace CompressorWithResx {
	public class Program {
		internal static int Main(string[] args) {
			Console.WriteLine("START");
			Console.WriteLine(Properties.Resources.TestString);
			Console.WriteLine("END");
			return 42;
		}
	}
}
