using System;

namespace RenameDynamicParameter {
	public class FieldTestClass {
		private int _storage;

		private void FieldTestMethod() => Console.WriteLine("Field Value: " + _storage);

		public void TestDynamic() {
			_storage = (dynamic)GetInteger();
			FieldTestMethod();
		}

		private static object GetInteger() => 1;
	}
}
