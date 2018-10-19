using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CompileResx.Test {
	public sealed class CompileRegexTest {
		private readonly ITestOutputHelper outputHelper;

		public CompileRegexTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(OptimizeAndExecuteTestData))]
		[Trait("Category", "Optimization")]
		[Trait("Optimization", "compile regex")]
		public async Task OptimizeAndExecuteTest(string framework) {
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "CompileRegex.exe");
			var outputFile = Path.Combine(outputDir, "CompileRegex.exe");
			FileUtilities.ClearOutput(outputFile);

			var recordedResult = await ExecuteTestApplication(inputFile, RecordOutput);

			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};

			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("compile regex")
			});

			proj.Add(new ProjectModule() { Path = inputFile });

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			await ExecuteTestApplication(outputFile, async stdout => {
				await VerifyOutput(recordedResult, stdout);
				return true;
			});

			FileUtilities.ClearOutput(outputFile);
		}

		private async Task<TResult> ExecuteTestApplication<TResult>(string file, Func<StreamReader, Task<TResult>> outputHandler) {
			var info = new ProcessStartInfo(file) {
				RedirectStandardOutput = true,
				StandardOutputEncoding = Encoding.UTF8,
				UseShellExecute = false
			};
			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				var result = await outputHandler(stdout);
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);

				return result;
			}
		}

		private async Task<IReadOnlyList<(string currentTest, string line)>> RecordOutput(StreamReader reader) {
			var result = new List<(string currentTest, string line)>();

			string line = null;
			string currentTest = "";
			while ((line = await reader.ReadLineAsync()) != null) {
				if (line.StartsWith("START TEST: "))
					currentTest = line.Substring(12);
				else if (line.Equals("END"))
					currentTest = "";

				result.Add((currentTest, line));
			}

			return result;
		}

		private async Task VerifyOutput(IReadOnlyList<(string currentTest, string line)> expected, StreamReader actual) {
			foreach (var expectedResult in expected) {
				try {
					Assert.Equal(expectedResult.line, await actual.ReadLineAsync());
				}
				catch {
					if (!string.IsNullOrWhiteSpace(expectedResult.currentTest))
						outputHelper.WriteLine("Failure in test: " + expectedResult.currentTest);
					throw;
				}
			}
		}

		public static IEnumerable<object[]> OptimizeAndExecuteTestData() {
			foreach (var framework in new string[] { "net20", "net40", "net471" })
				yield return new object[] { framework };
		}
	}
}
