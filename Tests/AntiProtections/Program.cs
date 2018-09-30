using System;
using System.Threading;

namespace AntiProtections {
	public class Program {
		internal static int Main(string[] args) {
			Thread.Sleep(1000); // Wait a second because some of the protections need that time to spin up.

			var t = System.Diagnostics.Process.GetCurrentProcess().Threads;
			Console.WriteLine("START");
			Console.WriteLine(Properties.Resources.Test);
			Console.WriteLine("END");
			return 42;
		}
	}
}
