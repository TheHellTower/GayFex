using System;

namespace RenameDynamicParameter {
	public class ConstructorTestClass {
		private string _message;

		private ConstructorTestClass(int value) : this("Ctor Integer Value: " + value) { }
		private ConstructorTestClass(string message) => _message = message;

		private void WriteMessage() => Console.WriteLine(_message);

		public static void TestInteger() {
			var instance = new ConstructorTestClass((dynamic)GetInteger());
			instance.WriteMessage();
		}

		public static void TestString() {
			var instance = new ConstructorTestClass((dynamic)GetString());
			instance.WriteMessage();
		}

		private static object GetInteger() => 1;
		private static object GetString() => "Ctor String Value";
	}
}
