using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public class AntiProtectionsTest {
		protected ITestOutputHelper OutputHelper { get; }

		protected AntiProtectionsTest(ITestOutputHelper outputHelper) =>
			OutputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		protected ILogger GetLogger() => new XunitLogger(OutputHelper);

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

			var info = new ProcessStartInfo() {
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			if (outputFile.EndsWith(".dll")) {
				info.FileName = "dotnet";
				info.Arguments = '"' + outputFile + '"';
			}
			else
				info.FileName = outputFile;

			OutputHelper.WriteLine("Executing test application: {0} {1}", info.FileName, info.Arguments);

			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				var stderr = process.StandardError;
				try {
					Assert.Equal("START", await stdout.ReadLineAsync());
					Assert.Equal("This is a test.", await stdout.ReadLineAsync());
					Assert.Equal("END", await stdout.ReadLineAsync());
					Assert.Empty(await stdout.ReadToEndAsync());
					Assert.Empty(await stderr.ReadToEndAsync());
				} catch {
					var cnt = 0;
					while (!process.HasExited && ++cnt < 10) {
						await Task.Delay(500);
					}
					OutputHelper.WriteLine("Remaining output: {0}", await stdout.ReadToEndAsync());
					OutputHelper.WriteLine("Remaining error: {0}", await stderr.ReadToEndAsync());
					if (process.HasExited)
						OutputHelper.WriteLine("Process exit code: {0:d}", process.ExitCode);
					else
						OutputHelper.WriteLine("Process has not exited.");
					throw;
				}

				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}

		protected static string GetExecutableName(string targetFramework) =>
			targetFramework.StartsWith("netstandard") || targetFramework.StartsWith("netcoreapp") ? "AntiProtections.dll" : "AntiProtections.exe";


		protected static string GetInputAssembly(ConfuserProject project, string targetFramework) =>
			Path.Combine(project.BaseDirectory, GetExecutableName(targetFramework));

		protected static IEnumerable<string> GetTargetFrameworks() => new string[] { "net20", "net40", "net471" };
	}
}
