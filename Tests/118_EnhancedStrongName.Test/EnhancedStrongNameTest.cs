using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace EnhancedStrongName.Test {
	public class EnhancedStrongNameTest {
		private readonly ITestOutputHelper outputHelper;

		public EnhancedStrongNameTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]		
        [Trait("Category", "core")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/118")]
		public async Task SignatureMismatch() {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp");
			var inputFile = Path.Combine(baseDir, "118_EnhancedStrongName.exe");
			var outputFile = Path.Combine(outputDir, "118_EnhancedStrongName.exe");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
			proj.Add(new ProjectModule() {
				Path = inputFile,
				SNSigKeyPath = Path.Combine(baseDir, "SignatureKey.snk"),
				SNPubSigKeyPath = Path.Combine(baseDir, "SignaturePubKey.snk"),
				SNKeyPath = Path.Combine(baseDir, "IdentityKey.snk"),
				SNPubKeyPath = Path.Combine(baseDir, "IdentityPubKey.snk")
			});

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
				Assert.Equal("My strong key token: 79A18AF4CEA8A9BD", await stdout.ReadLineAsync());
				Assert.Equal("My signature is valid!", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}
	}
}
