using System;
using SignatureMismatch2Helper;

namespace SignatureMismatch {
	public interface IInterface
	{
		void TestMethod(Base b);
	}

	public class Class : IInterface
	{
		public void TestMethod(Base b) => Console.WriteLine(b.Name);
	}

	public class Derived : Base
	{
		public override string Name => "Derived";
	}

	public class Program {
		static int Main(string[] args) {
			Console.WriteLine("START");
			new Class().TestMethod(new Derived());
			Console.WriteLine("END");

			return 42;
		}
	}
}
