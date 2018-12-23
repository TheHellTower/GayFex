using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace WpfRenaming.Test {
	public class ProcessWpfTest {
		private readonly ITestOutputHelper outputHelper;

		public ProcessWpfTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		/// <see cref="https://github.com/mkaring/ConfuserEx/issues/1"/>
		[Theory]
		[MemberData(nameof(ProcessWithoutObfuscationTestData))]
		[Trait("Category", "Analysis")]
		[Trait("Protection", "rename")]
		public async Task ProcessWithoutObfuscationTest(string framework) {
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
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
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));
		}

		[Theory]
		[MemberData(nameof(ProcessWithoutObfuscationTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "WPF")]
		public async Task ProcessWithObfuscationTest(string framework) {
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "WpfRenaming.dll");
			var outputFile = Path.Combine(outputDir, "WpfRenaming.dll");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
			proj.Add(new ProjectModule() { Path = inputFile });
			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("rename")
			});


			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));
		}

		public static IEnumerable<object[]> ProcessWithoutObfuscationTestData() {
			foreach (var framework in new string[] { "net40", "net471" })
				yield return new object[] { framework };
		}
	}
}
