using System.Collections.Generic;
using System.Reflection;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Services;
using dnlib.DotNet;
using Moq;
using Xunit;

namespace Confuser.Renamer.Test.Analyzers {
	public sealed class ReflectionAnalyzerTest {
		private string _referenceField;

		private string ReferenceProperty { get; }

		private void TestReferenceMethod1() {
			var method1 = typeof(ReflectionAnalyzerTest).GetMethod(nameof(TestReferenceMethod1));
			Assert.Null(method1);
			var method2 = typeof(ReflectionAnalyzerTest).GetMethod(nameof(TestReferenceMethod1), BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(method2);
		}
		
#pragma warning disable xUnit1013 // Public method should be marked as test
		public void TestReferenceField1() {
			var field1 = typeof(ReflectionAnalyzerTest).GetField(nameof(_referenceField));
			Assert.Null(field1);
			var field2 = typeof(ReflectionAnalyzerTest).GetField(nameof(_referenceField), BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(field2);
		}

		public void TestReferenceProperty1() {
			var prop1 = typeof(ReflectionAnalyzerTest).GetProperty(nameof(ReferenceProperty));
			Assert.Null(prop1);
			var prop2 = typeof(ReflectionAnalyzerTest).GetProperty(nameof(ReferenceProperty), BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(prop2);
		}
#pragma warning restore xUnit1013 // Public method should be marked as test

		[Fact]
		public void TestReferenceMethod1Test() {
			TestReferenceMethod1();

			var moduleDef = LoadTestModuleDef();
			var thisTypeDef = moduleDef.Find("Confuser.Renamer.Test.Analyzers.ReflectionAnalyzerTest", false);
			var refMethod = thisTypeDef.FindMethod(nameof(TestReferenceMethod1));

			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refMethod, false));
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refMethod, false));

			var traceService = new TraceService();
			var analyzer = new ReflectionAnalyzer();
			analyzer.Analyze(context, nameService, traceService, new List<ModuleDef>() { moduleDef }, refMethod);

			Mock.Get(nameService).VerifyAll();
		}

		[Fact]
		public void TestReferenceField1Test() {
			TestReferenceField1();

			var moduleDef = LoadTestModuleDef();
			var thisTypeDef = moduleDef.Find("Confuser.Renamer.Test.Analyzers.ReflectionAnalyzerTest", false);
			var refMethod = thisTypeDef.FindMethod(nameof(TestReferenceField1));
			var refField = thisTypeDef.FindField(nameof(_referenceField));
			
			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refField, false));
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refField, false));

			var traceService = new TraceService();
			var analyzer = new ReflectionAnalyzer();
			analyzer.Analyze(context, nameService, traceService, new List<ModuleDef>() { moduleDef }, refMethod);

			Mock.Get(nameService).VerifyAll();
		}

		[Fact]
		public void TestReferenceProperty1Test() {
			TestReferenceProperty1();

			var moduleDef = LoadTestModuleDef();
			var thisTypeDef = moduleDef.Find("Confuser.Renamer.Test.Analyzers.ReflectionAnalyzerTest", false);
			var refMethod = thisTypeDef.FindMethod(nameof(TestReferenceProperty1));
			var refProp = thisTypeDef.FindProperty(nameof(ReferenceProperty));
			
			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refProp, false));
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refProp, false));

			var traceService = new TraceService();
			var analyzer = new ReflectionAnalyzer();
			analyzer.Analyze(context, nameService, traceService, new List<ModuleDef>() { moduleDef }, refMethod);

			Mock.Get(nameService).VerifyAll();
		}

		private static ModuleDef LoadTestModuleDef() {
			var asmResolver = new AssemblyResolver { EnableTypeDefCache = true };
			asmResolver.DefaultModuleContext = new ModuleContext(asmResolver);
			var options = new ModuleCreationOptions(asmResolver.DefaultModuleContext) {
				TryToLoadPdbFromDisk = false
			};
			return ModuleDefMD.Load(typeof(VTableTest).Module, options);
		}
	}
}
