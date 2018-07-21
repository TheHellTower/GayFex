using System;
using System.Linq;

namespace TypeScrambler {
	public class Program {
		internal static int Main(string[] args) {
			Console.WriteLine("START");
			TestClass.WriteTextToConsole();
			Console.WriteLine(TestClass.GetTextStatic());

			var instance = new TestClass();
			Console.WriteLine(instance.GetText());

			Console.WriteLine(instance.GetTextFromGeneric("Text from generic method".AsEnumerable()));

			var genericInstance = new GenericClass<string>();
			Console.WriteLine(genericInstance.GetReverse("ssalc cireneg morf tseT").ToString());

			Console.WriteLine(Properties.Resources.Test);
			Console.WriteLine("END");
			return 42;
		}
	}
}
