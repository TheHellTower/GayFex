using System.Collections.Generic;
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
		[MemberData(nameof(AntiTamperTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti tamper")]
		public async Task ProtectAntiTamperAndExecute(string antiTamperMode, string framework) {
			var proj = CreateProject(framework);
			var inputFile = GetInputAssembly(proj, framework);
			var outputFile = Path.Combine(proj.OutputDirectory, GetExecutableName(framework));
			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("anti tamper") {
					{ "mode", antiTamperMode }
				}
			});
			proj.Add(new ProjectModule() { Path = inputFile, SNKeyPath = GetKeyFile() });

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = ConfigureLogging()
			};

			FileUtilities.ClearOutput(outputFile);
			await ConfuserEngine.Run(parameters);
			await VerifyTestApplication(inputFile, outputFile);
		}

		public static IEnumerable<object[]> AntiTamperTestData() {
			foreach (var framework in GetTargetFrameworks())
				foreach (var mode in new string[] { "Normal", "JIT" })
					yield return new object[] { mode, framework };
		}
	}
}
