using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace ConstantProtection.Test {
	public sealed class ConstantProtectionTest {
		private readonly ITestOutputHelper outputHelper;

		public ConstantProtectionTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(ProtectAndExecuteTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "constants")]
		public async Task ProtectAndExecuteTest(string framework, string modeKey, string compressor, bool cfgKey, string elementsKey) {
			var key = Path.Combine(Environment.CurrentDirectory, "Confuser.Test.snk");
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "ConstantProtection.exe");
			var outputFile = Path.Combine(outputDir, "ConstantProtection.exe");

			Assert.True(File.Exists(key));
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};

			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("constants") {
					{ "mode", modeKey },
					{ "compressor", compressor },
					{ "cfg", cfgKey ? "true" : "false" },
					{ "elements", elementsKey }
				}
			});

			proj.Add(new ProjectModule() { Path = inputFile, SNKeyPath = key });

			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			var recordedResult = await ProcessUtilities.ExecuteTestApplication(inputFile, RecordOutput, outputHelper).ConfigureAwait(true);

			await ConfuserEngine.Run(parameters).ConfigureAwait(true);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			var result = await ProcessUtilities.ExecuteTestApplication(outputFile, async stdout => {
				await VerifyOutput(recordedResult.Result, stdout).ConfigureAwait(true);
			}, outputHelper).ConfigureAwait(true);
			Assert.Equal(42, result);

			FileUtilities.ClearOutput(outputFile);
		}

		private async Task<IReadOnlyList<string>> RecordOutput(StreamReader reader) {
			var result = new List<string>();

			string line;
			while ((line = await reader.ReadLineAsync().ConfigureAwait(true)) != null) {
				result.Add(line);
			}

			return result;
		}

		private async Task VerifyOutput(IReadOnlyList<string> expected, StreamReader actual) {
			foreach (var expectedResult in expected) {
				Assert.Equal(expectedResult, await actual.ReadLineAsync().ConfigureAwait(true));
			}
		}

		public static IEnumerable<object[]> ProtectAndExecuteTestData() {
			foreach (var framework in new string[] { "net20", "net40", "net471" })
				foreach (var mode in new string[] { "Normal", "Dynamic", "x86" })
					foreach (var compressor in new string[] { "None", "Deflate", "Lzma", "Lz4" })
						foreach (var cfg in new bool[] { false, true })
							foreach (var encodeStrings in new string[] { "", "S" })
								foreach (var encodeNumbers in new string[] { "", "N" })
									foreach (var encodePrimitives in new string[] { "", "P" })
										foreach (var encodeInitializers in new string[] { "", "I" })
											yield return new object[] { framework, mode, compressor, cfg, encodeStrings + encodeNumbers + encodePrimitives + encodeInitializers };
		}
	}
}
