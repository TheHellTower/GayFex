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
using System.Linq;

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
			var key = Path.Combine(Environment.CurrentDirectory, "Confuser.Test.snk");
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "CompileRegex.exe");
			var outputFile = Path.Combine(outputDir, "CompileRegex.exe");

			Assert.True(File.Exists(key));
			FileUtilities.ClearOutput(outputFile);

			var recordedResult = await ProcessUtilities.ExecuteTestApplication(inputFile, RecordOutput, outputHelper);

			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir,
				Debug = true
			};

			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("compile regex")
			});

			proj.Add(new ProjectModule() { Path = inputFile, SNKeyPath = key });

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			Assert.True(await ConfuserEngine.Run(parameters));

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			await ProcessUtilities.ExecuteTestApplication(outputFile, async stdout => {
				await VerifyOutput(recordedResult.Result, stdout);
			}, outputHelper);

			FileUtilities.ClearOutput(outputFile);
		}

		private async Task<IReadOnlyList<(string currentTest, string line)>> RecordOutput(StreamReader reader) {
			var result = new List<(string currentTest, string line)>();

			string currentTest = "";
			string line;
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

		public static IEnumerable<object[]> OptimizeAndExecuteTestData() =>
			from framework in "net20;net35;net40;net48".Split(';')
			select new object[] { framework };
	}
}
