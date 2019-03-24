using System;
using System.Collections.Generic;
using dnlib.DotNet;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Renamer.Test {
	public class VTableTest {
		private readonly ITestOutputHelper outputHelper;

		public VTableTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/34")]
		public void DuplicatedMethodSignatureTest() {
			var asmResolver = new AssemblyResolver();
			asmResolver.EnableTypeDefCache = true;
			asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
			var options = new ModuleCreationOptions(asmResolver.DefaultModuleContext) {
				TryToLoadPdbFromDisk = false
			};
			var moduleDef = ModuleDefMD.Load(typeof(VTableTest).Module, options);
			var refClassTypeDef = moduleDef.Find("Confuser.Renamer.Test.VTableTestRefClass", false);

			Assert.NotNull(refClassTypeDef);
			var vTableStorage = new VTableStorage(new XUnitLogger(outputHelper));
			var refClassVTable = vTableStorage.GetVTable(refClassTypeDef);
			Assert.NotNull(refClassVTable);
		}
	}

	internal class VTableTestRefClass : VTableTestRefInterface<string> {
		public void TestMethod(List<string> values) { }
	}

	internal interface VTableTestRefInterface<T> {
		void TestMethod(List<string> values);
		void TestMethod(List<T> values);
	}
}
