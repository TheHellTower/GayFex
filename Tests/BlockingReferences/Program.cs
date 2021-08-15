using System;
using BlockingReferencesHelper;

namespace BlockingReferences {
	public interface IBaseInterface {
		string Method();
	}

	public class Implementation1 : BaseImplementation<string>, IBaseInterface {
	}

	public class Implementation2 : BaseImplementation<string>, IBaseInterface {
		public override string Method() => "Implementation2";
	}

	public static class Program {
		public static int Main() {
			Console.WriteLine("START");
			Console.WriteLine(new Implementation1().Method());
			Console.WriteLine(new Implementation2().Method());
			Console.WriteLine("END");

			return 42;
		}
	}
}
