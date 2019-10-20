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

namespace ConstantProtection.Test {
	public sealed class ConstantProtectionTest {
		private readonly ITestOutputHelper outputHelper;

		public ConstantProtectionTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(ProtectAndExecuteTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "constants")]
		public async Task ProtectAndExecuteTest(string framework, string modeKey, bool cfgKey, string elementsKey) {
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
					{ "cfg", cfgKey ? "true" : "false" },
					{ "elements", elementsKey }
				}
			});

			proj.Add(new ProjectModule() { Path = inputFile, SNKeyPath = key });


			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));

			var result = await ProcessUtilities.ExecuteTestApplication(outputFile, async (stdout) => {
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("123456", await stdout.ReadLineAsync());
				Assert.Equal("3", await stdout.ReadLineAsync());
				Assert.Equal("Test3", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
			}, outputHelper);
			Assert.Equal(42, result);

			FileUtilities.ClearOutput(outputFile);
		}

		public static IEnumerable<object[]> ProtectAndExecuteTestData() {
			foreach (var framework in GetTargetFrameworks())
				foreach (var mode in new string[] { "Normal", "Dynamic", "x86" })
					foreach (var cfg in new bool[] { false, true })
						foreach (var encodeStrings in new string[] { "", "S" })
							foreach (var encodeNumbers in new string[] { "", "N" })
								foreach (var encodePrimitives in new string[] { "", "P" })
									foreach (var encodeInitializers in new string[] { "", "I" })
										yield return new object[] { framework, mode, cfg, encodeStrings + encodeNumbers + encodePrimitives + encodeInitializers };
		}

#if CORE_RUNTIME
		private static IEnumerable<string> GetTargetFrameworks() => new string[] { "net40", "net48" };
#else
		private static IEnumerable<string> GetTargetFrameworks() => new string[] { "net20", "net40", "net48" };
#endif
	}
}
