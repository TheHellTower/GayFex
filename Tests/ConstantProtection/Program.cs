using System;

namespace ConstantProtection {
	public class Program {
		internal static int Main(string[] args) {
			var data = new int[] { 1, 2, 3, 4 };
			var data2 = new string[] { "Test1", "Test2", "Test3", "Test4" };

			Console.WriteLine("START");
			Console.WriteLine(123456.ToString());
			Console.WriteLine(data[2]);
			Console.WriteLine(data2[2]);
			Console.WriteLine("END");
			return 42;
		}
	}
}
