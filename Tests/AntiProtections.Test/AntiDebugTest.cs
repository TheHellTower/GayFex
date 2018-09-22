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
	public sealed class AntiDebugTest : AntiProtectionsTest {
		public AntiDebugTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[InlineData("safe")]
		[InlineData("win32")]
		[InlineData("antinet")]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti debug")]
		public async Task ProtectAntiDebugAndExecute(string antiDebugMode) {
			var proj = CreateProject();
			var inputFile = Path.Combine(proj.BaseDirectory, ExecutableFile);
			var outputFile = Path.Combine(proj.OutputDirectory, ExecutableFile);
			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("anti debug") {
					{ "mode", antiDebugMode }
				}
			});
			proj.Add(new ProjectModule() { Path = inputFile });

			var parameters = new ConfuserParameters {
				Project = proj,
				Logger = GetLogger()
			};

			FileUtilities.ClearOutput(outputFile);
			await ConfuserEngine.Run(parameters);
			await VerifyTestApplication(inputFile, outputFile);
		}
	}
}
