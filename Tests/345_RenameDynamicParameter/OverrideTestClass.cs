using System;

namespace RenameDynamicParameter {
	public class OverrideTestClass : OverrideBaseTestClass {
		protected override void OverrideTestMethod(string strobj) => Console.WriteLine("Override String: " + strobj);

		public void TestInteger() => OverrideTestMethod((dynamic)GetInteger());
		public void TestString() => OverrideTestMethod((dynamic)GetString());

		private static object GetInteger() => 1;
		private static object GetString() => "Test";
	}
}
