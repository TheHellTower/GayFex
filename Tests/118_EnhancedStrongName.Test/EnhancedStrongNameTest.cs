using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace EnhancedStrongName.Test {
	public class EnhancedStrongNameTest {
		private ITestOutputHelper OutputHelper { get; }

		public EnhancedStrongNameTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(SignEnhancedStrongNameTestData))]
        [Trait("Category", "core")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/118")]
		[SuppressMessage("Microsoft.Reliability", "CA2007:DoNotDirectlyAwaitATaskAnalyzer", Justification = "Default value is okay for unit test.")]
		public async Task SignEnhancedStrongNameTest(string framework) {
			var workDir = Environment.CurrentDirectory;
			var baseDir = Path.Combine(workDir, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "118_EnhancedStrongName.exe");
			var outputFile = Path.Combine(outputDir, "118_EnhancedStrongName.exe");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
			proj.Add(new ProjectModule() {
				Path = inputFile,
				SNSigKeyPath = Path.Combine(workDir, "SignatureKey.snk"),
				SNPubSigKeyPath = Path.Combine(workDir, "SignaturePubKey.snk"),
				SNKeyPath = Path.Combine(workDir, "IdentityKey.snk"),
				SNPubKeyPath = Path.Combine(workDir, "IdentityPubKey.snk")
			});

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(OutputHelper))
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

		public static IEnumerable<object[]> SignEnhancedStrongNameTestData() {
			yield return new[] { "net472" };
		}
	}
}
