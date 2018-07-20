using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace CompressorWithResx.Test {
	public class CompressTest {
		private readonly ITestOutputHelper outputHelper;

		public CompressTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[InlineData("false", "normal")]
		[InlineData("true", "normal")]
		[InlineData("false", "dynamic")]
		[InlineData("true", "dynamic")]
		public async Task CompressAndExecuteTest(string compatKey, string deriverKey) {
			var baseDir = Environment.CurrentDirectory;
			var outputFile = Path.Combine(baseDir, "testtmp", "CompressorWithResx.exe");
			ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = Path.Combine(baseDir, "testtmp"),
				Packer = new SettingItem<Packer>("compressor") {
					{ "compat", compatKey},
					{ "key", deriverKey }
				}
			};
			proj.Add(new ProjectModule() { Path = Path.Combine(baseDir, "CompressorWithResx.exe") });


			var parameters = new ConfuserParameters {
				Project = proj,
				Logger = new XunitLogger(outputHelper)
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));

			var info = new ProcessStartInfo(outputFile) {
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("Test", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			ClearOutput(outputFile);
		}

		private static void ClearOutput(string outputFile) {
			try {
				if (File.Exists(outputFile)) {
					File.Delete(outputFile);
				}
			}
			catch (UnauthorizedAccessException) { }
			var debugSymbols = Path.ChangeExtension(outputFile, "pdb");
			try {
				if (File.Exists(debugSymbols)) {
					File.Delete(debugSymbols);
				}
			}
			catch (UnauthorizedAccessException) { }
		}
	}
}
