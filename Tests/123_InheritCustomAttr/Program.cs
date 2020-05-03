using System;

namespace InheritCustomAttr {
	class Program {
		static int Main(string[] args) {
			Console.WriteLine("START");
			var e = new E();
			Console.WriteLine(e.T);
			// the following statement will crash the program after protection
			Console.WriteLine((Attribute.GetCustomAttributes(typeof(E).GetProperties()[0], typeof(MyAttribute))[0] as MyAttribute).Value);
			Console.WriteLine("END");
			return 42;
		}
	}
}
