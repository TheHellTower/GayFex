using System;

namespace ImplementationInBaseClass {
	internal abstract class MyBaseClass {
		public void MyMethod() => Console.WriteLine("Called " + nameof(MyMethod));
	}
}
