using System;

namespace RenameDynamicParameter {
	public static class OverloadTestClass {
		private static void OverloadTestMethod(int strobj) => Console.WriteLine("Overload Integer: " + strobj);
		private static void OverloadTestMethod(string strobj) => Console.WriteLine("Overload String: " + strobj);

		public static void TestInteger() => OverloadTestMethod((dynamic)GetInteger());
		public static void TestString() => OverloadTestMethod((dynamic)GetString());

		private static object GetInteger() => 1;
		private static object GetString() => "Test";
	}
}
