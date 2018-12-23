using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public class AntiProtectionsTest {
		protected ITestOutputHelper OutputHelper { get; }

		protected AntiProtectionsTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		protected Action<ILoggingBuilder> ConfigureLogging() => builder => builder.AddProvider(new XunitLogger(OutputHelper));

		protected ConfuserProject CreateProject(string framework) {
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			return new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};
		}

		protected async Task VerifyTestApplication(string inputFile, string outputFile) {
			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			var result = await ProcessUtilities.ExecuteTestApplication(outputFile, async (stdout) => {
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("This is a test.", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
			}, OutputHelper);

			FileUtilities.ClearOutput(outputFile);
		}

		protected static string GetExecutableName(string targetFramework) =>
			targetFramework.StartsWith("netstandard") || targetFramework.StartsWith("netcoreapp") ? "AntiProtections.dll" : "AntiProtections.exe";


		protected static string GetInputAssembly(ConfuserProject project, string targetFramework) =>
			Path.Combine(project.BaseDirectory, GetExecutableName(targetFramework));

		protected static IEnumerable<string> GetTargetFrameworks() => new string[] { "net20", "net40", "net471" };
	}
}
