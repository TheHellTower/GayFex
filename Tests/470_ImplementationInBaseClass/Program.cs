using System;

namespace ImplementationInBaseClass {
	internal class Program {
		internal static int Main(string[] args) {
			Console.WriteLine("START");

			var classA = new MyClassA();
			classA.MyMethod();

			var classB = new MyClassB();
			classB.MyMethod();

			var classB2 = new MyClassB2();
			classB2.MyMethod();

			var classC = new MyClassC();
			classC.MyMethod();

			Console.WriteLine("END");
			return 42;
		}
	}
}
