using System;
using SignatureMismatch2Helper;

namespace SignatureMismatch {
	public interface IInterface
	{
		void Method(External obj);
	}

	public class Class : IInterface
	{
		public void Method(External obj) => Console.WriteLine(obj.Name);
	}

	public class Program {
		static int Main(string[] args) {
			Console.WriteLine("START");
			new Class().Method(new External());
			Console.WriteLine("END");

			return 42;
		}
	}
}
