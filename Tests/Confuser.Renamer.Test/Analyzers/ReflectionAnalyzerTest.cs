using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Services;
using Confuser.UnitTest;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Renamer.Test.Analyzers {
	public sealed class ReflectionAnalyzerTest {
#pragma warning disable CS0649 // unused field
		[SuppressMessage("Style", "IDE0044:Add \"readonly\" modifier", Justification = "Correctly defined for test case.")]
		private string _referenceField;
#pragma warning restore CS0649
		private readonly ITestOutputHelper _outputHelper;

		public ReflectionAnalyzerTest(ITestOutputHelper outputHelper) =>
			_outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		private XunitLogger CreateLogger() => new XunitLogger(_outputHelper);

		private string ReferenceProperty { get; }

		private void TestReferenceMethod1() {
			var method1 = typeof(ReflectionAnalyzerTest).GetMethod(nameof(TestReferenceMethod1));
			Assert.Null(method1);
			var method2 = typeof(ReflectionAnalyzerTest).GetMethod(nameof(TestReferenceMethod1), BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(method2);
			var method3 = typeof(ReflectionAnalyzerTest).GetMethod(nameof(TestReferenceMethod1), BindingFlags.NonPublic | BindingFlags.Instance, null, CallingConventions.Standard, new Type[] { typeof(string) }, null);
			Assert.Null(method3);
		}

		[SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "It's not a test!")]
		public void TestReferenceField1() {
			var field1 = typeof(ReflectionAnalyzerTest).GetField(nameof(_referenceField));
			Assert.Null(field1);
			var field2 = typeof(ReflectionAnalyzerTest).GetField(nameof(_referenceField), BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(field2);
		}

		[SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "It's not a test!")]
		public void TestReferenceProperty1() {
			var prop1 = typeof(ReflectionAnalyzerTest).GetProperty(nameof(ReferenceProperty));
			Assert.Null(prop1);
			var prop2 = typeof(ReflectionAnalyzerTest).GetProperty(nameof(ReferenceProperty), BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(prop2);
		}

		[Fact]
		public void TestReferenceMethod1Test() {
			TestReferenceMethod1();

			var moduleDef = Helpers.LoadTestModuleDef();
			var thisTypeDef = moduleDef.Find("Confuser.Renamer.Test.Analyzers.ReflectionAnalyzerTest", false);
			var refMethod = thisTypeDef.FindMethod(nameof(TestReferenceMethod1));

			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refMethod, false));
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refMethod, false));
			Mock.Get(nameService).Setup(s => s.GetReferences(context, refMethod)).Returns(new List<INameReference>());
			Mock.Get(context).Setup(c => c.Modules).Returns(ImmutableArray.Create(moduleDef));

			var traceService = new TraceService();
			var analyzer = new ReflectionAnalyzer();
			analyzer.Analyze(context, nameService, traceService, CreateLogger(), refMethod);

			Mock.Get(nameService).VerifyAll();
		}

		[Fact]
		public void TestReferenceField1Test() {
			TestReferenceField1();

			var moduleDef = Helpers.LoadTestModuleDef();
			var thisTypeDef = moduleDef.Find("Confuser.Renamer.Test.Analyzers.ReflectionAnalyzerTest", false);
			var refMethod = thisTypeDef.FindMethod(nameof(TestReferenceField1));
			var refField = thisTypeDef.FindField(nameof(_referenceField));

			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refField, false));
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refField, false));
			Mock.Get(context).Setup(c => c.Modules).Returns(ImmutableArray.Create(moduleDef));

			var traceService = new TraceService();
			var analyzer = new ReflectionAnalyzer();

			analyzer.Analyze(context, nameService, traceService, CreateLogger(), refMethod);

			Mock.Get(nameService).VerifyAll();
		}

		[Fact]
		public void TestReferenceProperty1Test() {
			TestReferenceProperty1();

			var moduleDef = Helpers.LoadTestModuleDef();
			var thisTypeDef = moduleDef.Find("Confuser.Renamer.Test.Analyzers.ReflectionAnalyzerTest", false);
			var refMethod = thisTypeDef.FindMethod(nameof(TestReferenceProperty1));
			var refProp = thisTypeDef.FindProperty(nameof(ReferenceProperty));

			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refProp, false));
			Mock.Get(nameService).Setup(s => s.SetCanRename(context, refProp, false));
			Mock.Get(nameService).Setup(s => s.GetReferences(context, It.IsAny<object>())).Returns(new List<INameReference>());
			Mock.Get(context).Setup(c => c.Modules).Returns(ImmutableArray.Create(moduleDef));

			var traceService = new TraceService();
			var analyzer = new ReflectionAnalyzer();
			analyzer.Analyze(context, nameService, traceService, CreateLogger(), refMethod);

			Mock.Get(nameService).VerifyAll();
		}
	}
}
