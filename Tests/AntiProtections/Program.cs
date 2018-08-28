using System;

namespace AntiProtections {
	public class Program {
		internal static int Main(string[] args) {
			var t = System.Diagnostics.Process.GetCurrentProcess().Threads;
			Console.WriteLine("START");
			Console.WriteLine(Properties.Resources.Test);
			Console.WriteLine("END");
			return 42;
		}
	}
}
