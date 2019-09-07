using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace SignatureMismatch.Test {
	public class SignatureMismatchTest {
		private readonly ITestOutputHelper outputHelper;

		public SignatureMismatchTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]		
        [Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/78")]
		public async Task SignatureMismatch() {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp");
			var inputFile = Path.Combine(baseDir, "78_SignatureMismatch.exe");
			var outputFile = Path.Combine(outputDir, "78_SignatureMismatch.exe");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
			proj.Add(new ProjectModule() { Path = inputFile });

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
				Assert.Equal("Dictionary created", await stdout.ReadLineAsync());
				Assert.Equal("Dictionary count: 1", await stdout.ReadLineAsync());
				Assert.Equal("[Test1] = Test2", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}
	}
}
