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

namespace RefProxyProtection.Test {
	public sealed class RefProxyTest {
		private readonly ITestOutputHelper outputHelper;

		public RefProxyTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(ProtectAndExecuteTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "ref proxy")]
		public async Task ProtectAndExecuteTest(string framework, string modeKey, string encodingKey, string internalKey, string typeErasureKey) {
			var key = Path.Combine(Environment.CurrentDirectory, "Confuser.Test.snk");
			var baseDir = Path.Combine(Environment.CurrentDirectory, framework);
			var outputDir = Path.Combine(baseDir, "testtmp_" + Guid.NewGuid().ToString());
			var inputFile = Path.Combine(baseDir, "RefProxyProtection.exe");
			var outputFile = Path.Combine(outputDir, "RefProxyProtection.exe");

			Assert.True(File.Exists(key));
			FileUtilities.ClearOutput(outputFile);
			var proj = new ConfuserProject {
				BaseDirectory = baseDir,
				OutputDirectory = outputDir
			};

			proj.Rules.Add(new Rule() {
				new SettingItem<IProtection>("ref proxy") {
					{ "mode", modeKey },
					{ "encoding", encodingKey },
					{ "internal", internalKey },
					{ "typeErasure", typeErasureKey }
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
				Assert.Equal("dictTest[TestKey] = TestValue", await stdout.ReadLineAsync());
				Assert.Equal("END", await stdout.ReadLineAsync());
			}, outputHelper);
			Assert.Equal(42, result);

			FileUtilities.ClearOutput(outputFile);
		}

		public static IEnumerable<object[]> ProtectAndExecuteTestData() {
			foreach (var framework in new string[] { "net20", "net40", "net471" })
				foreach (var mode in new string[] { "Mild", "Strong" })
					foreach (var encoding in new string[] { "Normal", "Expression", "x86" })
						foreach (var internalAlso in new string[] { "true", "false" })
							foreach (var typeErasure in new string[] { "true", "false" })
								yield return new object[] { framework, mode, encoding, internalAlso, typeErasure };
		}
	}
}
