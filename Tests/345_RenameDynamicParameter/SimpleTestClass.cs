using System;

namespace RenameDynamicParameter {
	public static class SimpleTestClass {
		private static void SimpleTestMethod(object strobj) => Console.WriteLine(strobj);

		public static void TestDynamic() => SimpleTestMethod((dynamic)"dynamic message");
		public static void TestStatic() => SimpleTestMethod("static message");
	}
}
