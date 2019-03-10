using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace VisualBasicRenamingResx.Test {
	public sealed class RenamingTest {
		private readonly ITestOutputHelper outputHelper;

		public RenamingTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(ProtectAndExecuteTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/25")]
		public async Task ProtectAndExecuteTest(string framework) {
			var key = Path.Combine(Environment.CurrentDirectory, "Confuser.Test.snk");
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "VisualBasicRenamingResx.exe");
			var outputFile = Path.Combine(outputDir, "VisualBasicRenamingResx.exe");
			FileUtilities.ClearOutput(outputFile);

			Assert.True(File.Exists(key));
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir,
			};

			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("rename")
			});

			proj.Add(new ProjectModule() { Path = inputFile, SNKeyPath = key });

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			var info = new ProcessStartInfo(outputFile) {
				RedirectStandardOutput = true,
				UseShellExecute = false
			};

			var result = await ProcessUtilities.ExecuteTestApplication(outputFile, async (stdout) => {
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("Test (neutral)", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
			}, outputHelper);
			Assert.Equal(42, result);

			FileUtilities.ClearOutput(outputFile);
		}

		public static IEnumerable<object[]> ProtectAndExecuteTestData() {
			foreach (var framework in new string[] { "net40", "net471" })
				yield return new object[] { framework };
		}
	}
}
