using System;
using System.Runtime.CompilerServices;
using ApprovalTests;
using ApprovalTests.Core;
using ApprovalTests.Namers;
using ApprovalTests.Reporters;
using ApprovalTests.Reporters.ContinuousIntegration;
using ApprovalTests.Reporters.TestFrameworks;
using ApprovalTests.Reporters.Windows;
using ApprovalTests.Writers;
using Confuser.Core.Services;
using Confuser.UnitTest;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Xunit;
using Xunit.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

[assembly: UseReporter(typeof(VisualStudioReporter), typeof(AppVeyorReporter), typeof(XUnit2Reporter))]

namespace Confuser.Optimizations.TailCall {
	public class TailCallTest {
		private static Type ThisType => typeof(TailCallTest);

		private ITestOutputHelper OutputHelper { get; }

		public TailCallTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		// ReSharper disable once TailRecursiveCall
		// ReSharper disable once MemberCanBePrivate.Global
		public static int RecursiveSumTernary(int n, int sum) => n == 0 ? sum : RecursiveSumTernary(n - 1, sum + n);

		// ReSharper disable once TailRecursiveCall
		// ReSharper disable once MemberCanBePrivate.Global
		// ReSharper disable once ConvertIfStatementToReturnStatement
		public static int RecursiveSum(int n, int sum) {
			if (n == 0) return sum;
			return RecursiveSum(n - 1, sum + n);
		}

		// ReSharper disable once MemberCanBePrivate.Global
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

			using (var logger = new XunitLogger(OutputHelper))
				Assert.True(processMethod(testMethodDef, logger, new TestTraceService()));

			testMethodDef.Body.OptimizeBranches();
			testMethodDef.Body.OptimizeMacros();

			Approvals.Verify(WriteApprovalFile(testMethodDef), new ApprovalNamer(), Approvals.GetReporter());
		}

		private static IApprovalWriter WriteApprovalFile(MethodDef testMethodDef) =>
			WriterFactory.CreateTextWriter(DnlibUtilities.WriteBody(testMethodDef.Body));

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void RecursiveSumTailCallTest() => TestTailCallMethod(nameof(RecursiveSum), AddTailCallPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void RecursiveSumTernaryTailCallTest() => TestTailCallMethod(nameof(RecursiveSumTernary), AddTailCallPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void TailCallMethodTest() => TestTailCallMethod(nameof(TailCallMethod), AddTailCallPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void RecursiveSumRecurseTest() => TestTailCallMethod(nameof(RecursiveSum), OptimizeRecursionPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void RecursiveSumTernaryRecurseTest() => TestTailCallMethod(nameof(RecursiveSumTernary), OptimizeRecursionPhase.ProcessMethod);

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		public void TailOptimizeDnlib() {
			var dnLibModuleDef = ModuleDefMD.Load(typeof(ModuleDefMD).Module, new ModuleCreationOptions() {
				TryToLoadPdbFromDisk = false
			});

			foreach (var types in dnLibModuleDef.GetTypes())
				foreach (var methodDef in types.Methods)
					using (var logger = new XunitLogger(OutputHelper))
						OptimizeRecursionPhase.ProcessMethod(methodDef, logger);
		}

		[Fact]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", TailCallProtection.Id)]
		public void AddTailDnlib() {
			var dnLibModuleDef = ModuleDefMD.Load(typeof(ModuleDefMD).Module, new ModuleCreationOptions() {
				TryToLoadPdbFromDisk = false
			});
			
			var traceService = new TraceService();
			foreach (var types in dnLibModuleDef.GetTypes())
				foreach (var methodDef in types.Methods)
					using (var logger = new XunitLogger(OutputHelper))
						AddTailCallPhase.ProcessMethod(methodDef, logger, traceService);
		}


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
