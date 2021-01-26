using System.Reflection;

namespace ComplexClassStructureRename.Lib {
	[Obfuscation(Exclude = false, Feature = "-rename")]
	public class PublicClass1 : ITestEvents {
		public void FireLog(string message) { }
	}
}
