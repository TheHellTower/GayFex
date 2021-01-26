using System.Reflection;

namespace ComplexClassStructureRename.Lib {
	[Obfuscation(Exclude = false, Feature = "-rename")]
	public class PublicClass2 {
		readonly MyTest _test = new MyTest();

		public void Test() => _test.Test();
	}
}
