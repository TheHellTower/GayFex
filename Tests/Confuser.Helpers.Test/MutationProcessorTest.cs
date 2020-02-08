using System;
using System.Collections.Generic;
using System.IO;
using ApprovalTests;
using ApprovalTests.Core;
using ApprovalTests.Namers;
using ApprovalTests.Writers;
using Confuser.Core.Services;
using Confuser.UnitTest;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Helpers.Test {
	public class MutationProcessorTest {
		private static Type ThisType => typeof(MutationProcessorTest);

		private ITestOutputHelper OutputHelper { get; }

		public MutationProcessorTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		private void ProcessPlaceholder(string testMethod) {
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

			var mutationProcessor = new MutationProcessor(new TraceService(), thisModule) {
				PlaceholderProcessor = (module, method, args) => {
					var result = new List<Instruction>();
					result.Add(OpCodes.Ldc_I4_1.ToInstruction());
					result.AddRange(args);
					result.Add(OpCodes.Add.ToInstruction());
					return result;
				}
			};
			mutationProcessor.Process(testMethodDef);

			testMethodDef.Body.OptimizeBranches();
			testMethodDef.Body.OptimizeMacros();

			Assert.True(MaxStackCalculator.GetMaxStack(testMethodDef.Body.Instructions, testMethodDef.Body.ExceptionHandlers, out var newMaxStack));
			testMethodDef.Body.MaxStack = (ushort)newMaxStack;

			Approvals.Verify(WriteApprovalFile(testMethodDef), new ApprovalNamer(), Approvals.GetReporter());
		}

		private static IApprovalWriter WriteApprovalFile(MethodDef testMethodDef) =>
			WriterFactory.CreateTextWriter(DnlibUtilities.WriteBody(testMethodDef.Body));

		[Fact]
		public void PlaceholderAfterUsingTest() => ProcessPlaceholder(nameof(PlaceholderAfterUsing));

		private static int PlaceholderAfterUsing(int id) {
			using (Stream str = new MemoryStream()) {
				str.Position = 0;
			}
			return Mutation.Placeholder(id);
		}

		private sealed class ApprovalNamer : UnitTestFrameworkNamer {
#if DEBUG
			public override string Name => base.Name + ".Debug";
#endif
		}
	}
}
