using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public sealed class AntiDumpTest : AntiProtectionsTest {
		public AntiDumpTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(AntiDumpTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti dump")]
		public async Task ProtectAntiDumpAndExecute(string framework) {
			var proj = CreateProject(framework);
			var inputFile = GetInputAssembly(proj, framework);
			var outputFile = Path.Combine(proj.OutputDirectory, GetExecutableName(framework));
			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("anti dump")
			});
			proj.Add(new ProjectModule() { Path = inputFile });

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = ConfigureLogging()
			};

			FileUtilities.ClearOutput(outputFile);
			await ConfuserEngine.Run(parameters);
			await VerifyTestApplication(inputFile, outputFile);
		}

		public static IEnumerable<object[]> AntiDumpTestData() {
			foreach (var framework in GetTargetFrameworks())
				yield return new object[] { framework };
		}
	}
}
