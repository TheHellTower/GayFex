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
	public sealed class AntiDebugTest : AntiProtectionsTest {
		public AntiDebugTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(AntiDebugTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti debug")]
		public async Task ProtectAntiDebugAndExecute(string antiDebugMode, string framework) {
			var proj = CreateProject(framework);
			var inputFile = GetInputAssembly(proj, framework);
			OutputHelper.WriteLine("Input file: {0}", inputFile);
			Assert.True(File.Exists(inputFile));
			var outputFile = Path.Combine(proj.OutputDirectory, GetExecutableName(framework));
			OutputHelper.WriteLine("Output file: {0}", outputFile);
			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("anti debug") {
					{ "mode", antiDebugMode }
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

		public static IEnumerable<object[]> AntiDebugTestData() {
			foreach (var framework in GetTargetFrameworks())
				foreach (var mode in new string[] { "Safe", "Win32", "Antinet" })
					yield return new object[] { mode, framework };
		}
	}
}
