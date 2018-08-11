using System;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace WpfRenaming.Test {
	public class ProcessWpfTest {
		private readonly ITestOutputHelper outputHelper;

		public ProcessWpfTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
		
		/// <see cref="https://github.com/mkaring/ConfuserEx/issues/1"/>
		[Fact]
		[Trait("Category", "Analysis")]
		[Trait("Protection", "rename")]
		public async Task ProcessWithoutObfuscationTest() {
			var baseDir = Environment.CurrentDirectory;
			var outputDir = Path.Combine(baseDir, "testtmp");
			var inputFile = Path.Combine(baseDir, "WpfRenaming.dll");
			var outputFile = Path.Combine(outputDir, "WpfRenaming.dll");
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
		}
	}
}
