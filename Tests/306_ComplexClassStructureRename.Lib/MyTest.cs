namespace ComplexClassStructureRename.Lib {
	internal class MyTest {
		readonly InternalClass1 _test1 = new InternalClass1();
		readonly InternalClass2 _test2 = new InternalClass2();

		public void Test() {
			_test1.FireLog("test1 Hello");
			_test2.FireLog("test2 Hello");
		}

	}
}
