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
	public class AntiProtectionsTest {
		private readonly ITestOutputHelper outputHelper;

		protected const string ExecutableFile = "AntiProtections.exe";

		protected AntiProtectionsTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		protected ILogger GetLogger() => new XunitLogger(outputHelper);

		protected ConfuserProject CreateProject() {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			return new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
		}

		protected async Task VerifyTestApplication(string inputFile, string outputFile) {
			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			var info = new ProcessStartInfo(outputFile) {
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("This is a test.", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}
	}
}
