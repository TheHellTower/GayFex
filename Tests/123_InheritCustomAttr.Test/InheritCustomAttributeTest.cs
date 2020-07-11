using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Renamer;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace _123_InheritCustomAttr.Test {
	public class InheritCustomAttributeTest {
		private readonly ITestOutputHelper outputHelper;

		public InheritCustomAttributeTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(InheritCustomAttributeData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/123")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/161")]
		public async Task InheritCustomAttribute(string renameMode, bool flatten) {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "123_InheritCustomAttr.exe");
			var outputFile = Path.Combine(outputDir, "123_InheritCustomAttr.exe");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
			proj.Add(new ProjectModule() { Path = inputFile });
			proj.Rules.Add(new Rule() {
				new SettingItem<Protection>("rename") {
					{ "mode", renameMode },
					{ "flatten", flatten ? "True" : "False" }
				}
			});

			var parameters = new ConfuserParameters {
				Project = proj,
				Logger = new XunitLogger(outputHelper, l => Assert.False(l.StartsWith("[WARN]"), "Logged line may not start with [WARN]\r\n" + l))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			var info = new ProcessStartInfo(outputFile) {
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("Monday", await stdout.ReadLineAsync());
				Assert.Equal("43", await stdout.ReadLineAsync());
				Assert.Equal("1", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}

		public static IEnumerable<object[]> InheritCustomAttributeData() {
			foreach (var renameMode in new string[] { nameof(RenameMode.Unicode), nameof(RenameMode.ASCII), nameof(RenameMode.Letters), nameof(RenameMode.Debug) })
				foreach (var flatten in new bool[] { true, false })
					yield return new object[] { renameMode, flatten };
		}
	}
}
