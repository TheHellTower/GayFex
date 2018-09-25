using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public sealed class AntiTamperTest : AntiProtectionsTest {
		public AntiTamperTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[InlineData("normal")]
		[InlineData("anti")]
		[InlineData("jit", Skip = "Runtime Component of the JIT AntiTamper protection is broken.")]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti tamper")]
		public async Task ProtectAntiTamperAndExecute(string antiTamperMode) {
			var proj = CreateProject();
			var inputFile = Path.Combine(proj.BaseDirectory, ExecutableFile);
			var outputFile = Path.Combine(proj.OutputDirectory, ExecutableFile);
			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("anti tamper") {
					{ "mode", antiTamperMode }
				}
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
	}
}
