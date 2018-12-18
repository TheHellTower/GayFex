using System;
using ApprovalTests;
using ApprovalTests.Core;
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
using ApprovalTests.Writers;
using Confuser.Core.Services;
using Confuser.UnitTest;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Xunit;
using Xunit.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

[assembly: UseReporter(typeof(DiffReporter), typeof(AppVeyorReporter), typeof(XUnit2Reporter))]

namespace Confuser.Optimizations.TailCall {
	public class TailCallTest {
		private Type ThisType => typeof(TailCallTest);

		private ITestOutputHelper OutputHelper { get; }

		public TailCallTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		public static int RecursiveSum(int n, int sum) {
			if (n == 0) return sum;
			else return RecursiveSum(n - 1, sum + n);
		}

		public static double TailCallMethod(double value) => Math.Abs(value);


		private void TestTailCallMethod(string testMethod, Func<MethodDef, ILogger, bool> processMethod) =>
			TestTailCallMethod(testMethod, (m, l, ts) => processMethod(m, l));

		private void TestTailCallMethod(string testMethod, Func<MethodDef, ILogger, ITraceService, bool> processMethod) {
			var thisModule = ModuleDefMD.Load(ThisType.Module, new ModuleCreationOptions() {
				TryToLoadPdbFromDisk = false
			});
			Assert.NotNull(thisModule);

			var testMethodDef = thisModule
				.FindNormalThrow(ThisType.FullName)
				.FindMethod(testMethod);
			Assert.NotNull(testMethodDef);
			testMethodDef.Body.SimplifyMacros(testMethodDef.Parameters);
			testMethodDef.Body.SimplifyBranches();

			Assert.True(processMethod(testMethodDef, new XunitLogger(OutputHelper), new TestTraceService()));

			testMethodDef.Body.OptimizeBranches();
			testMethodDef.Body.OptimizeMacros();

			Approvals.Verify(WriteApprovalFile(testMethodDef), new ApprovalNamer(), Approvals.GetReporter());
		}

		private static IApprovalWriter WriteApprovalFile(MethodDef testMethodDef) =>
			WriterFactory.CreateTextWriter(DnlibUtilities.WriteBody(testMethodDef.Body));

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		public void RecursiveSumTailCallTest() => TestTailCallMethod(nameof(RecursiveSum), AddTailCallPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		public void TailCallMethodTest() => TestTailCallMethod(nameof(TailCallMethod), AddTailCallPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		public void RecursiveSumRecurseTest() => TestTailCallMethod(nameof(RecursiveSum), OptimizeRecursionPhase.ProcessMethod);

		private sealed class ApprovalNamer : UnitTestFrameworkNamer {
#if DEBUG
			public override string Name => base.Name + ".Debug";
#endif
		}

		private sealed class TestTraceService : ITraceService, IMethodTrace {
			public int[] TraceArguments(Instruction instr) => Array.Empty<int>();

			IMethodTrace ITraceService.Trace(MethodDef method) => this;
		}
	}
}
