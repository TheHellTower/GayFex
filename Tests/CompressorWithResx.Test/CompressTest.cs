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

namespace CompressorWithResx.Test {
	public sealed class CompressTest {
		private readonly ITestOutputHelper outputHelper;

		public CompressTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(CompressAndExecuteTestData))]
		[MemberData(nameof(CompressAndExecuteSkippedTestData), Skip = ".NET Framework 2.0 is not properly supported by the compressor.")]
		[Trait("Category", "Packer")]
		[Trait("Packer", "compressor")]
		public async Task CompressAndExecuteTest(string framework, string compatKey, string deriverKey, string resourceProtectionMode) {
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "CompressorWithResx.exe");
			var inputSatelliteFile = Path.Combine(baseDir, "de", "CompressorWithResx.resources.dll");
			var outputFile = Path.Combine(outputDir, "CompressorWithResx.exe");
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir,
				Packer = new SettingItem<IPacker>("compressor") {
					{ "compat", compatKey},
					{ "key", deriverKey }
				}
			};

			if (resourceProtectionMode != "none") {
				proj.Rules.Add(new Rule() {
					new SettingItem<IProtection>("resources") {
						{ "mode", resourceProtectionMode }
					}
				});
			}

			proj.Add(new ProjectModule() { Path = inputFile });
			proj.Add(new ProjectModule() { Path = inputSatelliteFile });


			var parameters = new ConfuserParameters {
				Project = proj,
				ConfigureLogging = builder => builder.AddProvider(new XunitLogger(outputHelper))
			};

			await ConfuserEngine.Run(parameters);

			Assert.True(File.Exists(outputFile));
			Assert.NotEqual(FileUtilities.ComputeFileChecksum(inputFile), FileUtilities.ComputeFileChecksum(outputFile));
			
			var result = await ProcessUtilities.ExecuteTestApplication(outputFile, async (stdout) => {
				Assert.Equal("START", await stdout.ReadLineAsync());
				Assert.Equal("Test (fallback)", await stdout.ReadLineAsync());
				Assert.Equal("Test (deutsch)", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
			}, outputHelper);
			Assert.Equal(42, result);

			FileUtilities.ClearOutput(outputFile);
		}

		public static IEnumerable<object[]> CompressAndExecuteTestData() {
			foreach (var framework in new string[] { "net40", "net471" })
				foreach (var data in CompressorParameterData(framework))
					yield return data;
		}

		public static IEnumerable<object[]> CompressAndExecuteSkippedTestData() {
			foreach (var framework in new string[] { "net20" })
				foreach (var data in CompressorParameterData(framework))
					yield return data;
		}

		private static IEnumerable<object[]> CompressorParameterData(string framework) {
			foreach (var compressorCompatKey in new string[] { "true", "false" })
				foreach (var compressorDeriveKey in new string[] { "normal", "dynamic" })
					foreach (var resourceProtectionMode in new string[] { "none", "normal", "dynamic" })
						yield return new object[] { framework, compressorCompatKey, compressorDeriveKey, resourceProtectionMode };
		}
	}
}
