using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace ConstantsInlining.Test {
	public class ConstantInliningTest {
		private readonly ITestOutputHelper outputHelper;

		public ConstantInliningTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "constants")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/193")]
		public async Task ConstantInlining() {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp");
			var files = new[] { "193_ConstantsInlining.exe", "193_ConstantsInlining.Lib.dll" };
			foreach (var file in files)
				FileUtilities.ClearOutput(Path.Combine(outputDir, file));

			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
			proj.Rules.Add(new Rule() {
				new SettingItem<Protection>("constants") {
					{ "elements", "S" }
				}
			});

			proj.AddRange(files.Select(f => new ProjectModule() { Path = Path.Combine(baseDir, f) }));

			var parameters = new ConfuserParameters {
				Project = proj,
				Logger = new XunitLogger(outputHelper)
			};

			await ConfuserEngine.Run(parameters);

			foreach (var file in files) {
				Assert.True(File.Exists(Path.Combine(outputDir, file)));
				Assert.NotEqual(
					FileUtilities.ComputeFileChecksum(Path.Combine(baseDir, file)),
					FileUtilities.ComputeFileChecksum(Path.Combine(outputDir, file)));
			}

			var info = new ProcessStartInfo(Path.Combine(outputDir, files[0])) {
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("From External", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}
			
			foreach (var file in files)
				FileUtilities.ClearOutput(Path.Combine(outputDir, file));
		}
	}
}
