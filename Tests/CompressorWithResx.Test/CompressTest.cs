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

namespace CompressorWithResx.Test {
	public sealed class CompressTest {
		private readonly ITestOutputHelper outputHelper;

		public CompressTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(CompressAndExecuteTestData))]
		[Trait("Category", "Packer")]
		[Trait("Packer", "compressor")]
		public async Task CompressAndExecuteTest(string compatKey, string deriverKey, string resourceProtectionMode) {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp");
			var inputFile = Path.Combine(baseDir, "CompressorWithResx.exe");
			var inputSatelliteFile = Path.Combine(baseDir, "de", "CompressorWithResx.resources.dll");
			var outputFile = Path.Combine(outputDir, "CompressorWithResx.exe");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir,
				Packer = new SettingItem<Packer>("compressor") {
					{ "compat", compatKey},
					{ "key", deriverKey }
				}
			};

			if (resourceProtectionMode != "none") {
				proj.Rules.Add(new Rule() {
					new SettingItem<Protection>("resources") {
						{ "mode", resourceProtectionMode }
					}
				});
			}

			proj.Add(new ProjectModule() { Path = inputFile });
			proj.Add(new ProjectModule() { Path = inputSatelliteFile });


			var parameters = new ConfuserParameters {
				Project = proj,
				Logger = new XunitLogger(outputHelper)
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
				Assert.Equal("Test (fallback)", await stdout.ReadLineAsync());
				Assert.Equal("Test (deutsch)", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}

		public static IEnumerable<object[]> CompressAndExecuteTestData() {
			foreach (var compressorCompatKey in new string[] { "true", "false" })
				foreach (var compressorDeriveKey in new string[] { "normal", "dynamic" })
					foreach (var resourceProtectionMode in new string[] { "none", "normal", "dynamic" })
						yield return new object[] { compressorCompatKey, compressorDeriveKey, resourceProtectionMode };
		}
	}
}
