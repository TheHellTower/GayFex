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

			var info = new ProcessStartInfo(outputFile) {
				RedirectStandardOutput = true,
				StandardOutputEncoding = Encoding.UTF8,
				UseShellExecute = false
			};
			using (var process = Process.Start(info)) {
				var stdout = process.StandardOutput;
				foreach (var expectedLine in ExpectedOutput()) {
					Assert.Equal(expectedLine, await stdout.ReadLineAsync());
				}
				Assert.Empty(await stdout.ReadToEndAsync());
				Assert.True(process.HasExited);
				Assert.Equal(42, process.ExitCode);
			}

			FileUtilities.ClearOutput(outputFile);
		}

		private static IEnumerable<string> ExpectedOutput() {
			yield return "START";
			yield return "Canonical matching: 'äöü' matches the pattern.";
			yield return "ECMAScript matching: 'äöü' does not match the pattern.";
			yield return "";
			yield return "Canonical matching: 'aou' matches the pattern.";
			yield return "ECMAScript matching: 'aou' matches the pattern.";
			yield return "";
			yield return "The Brooklyn Dodgers played in the National League in 1911, 1912, 1932-1957.";
			yield return "";
			yield return "The Brooklyn Dodgers played in the National League in 1911, 1912, 1932-1957.";
			yield return "The Chicago Cubs played in the National League in 1903-present.";
			yield return "The Detroit Tigers played in the American League in 1901-present.";
			yield return "The New York Giants played in the National League in 1885-1957.";
			yield return "The Washington Senators played in the American League in 1901-1960.";
			yield return "";
			yield return "Duplicate 'that' found at positions 8 and 13.";
			yield return "Duplicate 'the' found at positions 22 and 26.";
			yield return "";
			yield return "A duplicate 'that' at position 8 is followed by 'was'.";
			yield return "A duplicate 'the' at position 22 is followed by 'correct'.";
			yield return "";
			yield return "Input: \"<abc><mno<xyz>>\"";
			yield return "Match: \"<abc><mno<xyz>>\"";
			yield return "   Group 0: <abc><mno<xyz>>";
			yield return "      Capture 0: <abc><mno<xyz>>";
			yield return "   Group 1: <mno<xyz>>";
			yield return "      Capture 0: <abc>";
			yield return "      Capture 1: <mno<xyz>>";
			yield return "   Group 2: <xyz";
			yield return "      Capture 0: <abc";
			yield return "      Capture 1: <mno";
			yield return "      Capture 2: <xyz";
			yield return "   Group 3: >";
			yield return "      Capture 0: >";
			yield return "      Capture 1: >";
			yield return "      Capture 2: >";
			yield return "   Group 4: ";
			yield return "   Group 5: mno<xyz>";
			yield return "      Capture 0: abc";
			yield return "      Capture 1: xyz";
			yield return "      Capture 2: mno<xyz>";
			yield return "";
			yield return "END";
		}



		public static IEnumerable<object[]> OptimizeAndExecuteTestData() {
			foreach (var framework in new string[] { "net20", "net40", "net471" })
				yield return new object[] { framework };
		}
	}
}
