using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CompressorWithResx.Test {
	public sealed class CompressTest : TestBase {
		public CompressTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(CompressAndExecuteTestData))]
		[MemberData(nameof(CompressAndExecuteSkippedTestData), Skip = ".NET Framework 2.0 is not properly supported by the compressor.")]
		[Trait("Category", "Packer")]
		[Trait("Packer", "compressor")]
		public async Task CompressAndExecuteTest(string framework, string compatKey, string deriverKey, string resourceProtectionMode) =>
			await Run(
				framework,
				new[] {"CompressorWithResx.exe", Path.Combine("de", "CompressorWithResx.resources.dll")},
				new[] {"Test (fallback)", "Test (deutsch)"},
				resourceProtectionMode != "none"
					? new SettingItem<IProtection>("resources") {{"mode", resourceProtectionMode}}
					: null,
				$"_{compatKey}_{deriverKey}_{resourceProtectionMode}",
				packer: new SettingItem<IPacker>("compressor") {{"compat", compatKey}, {"key", deriverKey}});

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
