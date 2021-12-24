using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace MethodOverloading.Test {
	public class MethodOverloadingTest : TestBase {
		public MethodOverloadingTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(MethodOverloadingData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/230")]
		public async Task MethodOverloading(bool shortNames, bool preserveGenericParams) {
			var mappings = new Dictionary<string, string>();
			// Validate that the output is the same, for a specific seed accross two runs.
			await MethodOverloadingInternal(shortNames, preserveGenericParams, mappings, true);
			await MethodOverloadingInternal(shortNames, preserveGenericParams, mappings, false);
		}

		private async Task MethodOverloadingInternal(bool shortNames, bool preserveGenericParams, Dictionary<string, string> mappings, bool recordMapping) =>
			await Run(
				"net461",
				"MethodOverloading.exe",
				new[] {
					"1",
					"Hello world",
					"object",
					"2",
					"test",
					"5",
					"class",
					"class2",
					"class3",
					"class4",
					"class5",
					"BaseClassVirtualMethod",
					"ClassVirtualMethod",
					"ClassVirtualMethod"
				},
				new SettingItem<IProtection>("rename") {
					["mode"] = "decodable",
					["shortNames"] = shortNames.ToString().ToLowerInvariant(),
					["preserveGenericParams"] = preserveGenericParams.ToString().ToLowerInvariant()
				},
				(shortNames ? "_shortnames" : "_fullnames") + (preserveGenericParams ? "_preserveGenericParams" : ""),
				seed: "seed",
				postProcessAction: outputPath => {
					var symbolsPath = Path.Combine(outputPath, CoreConstants.SymbolsFileName);
					var symbols = File.ReadAllLines(symbolsPath).Select(line => {
						var parts = line.Split('\t');
						return new KeyValuePair<string, string>(parts[0], parts[1]);
					}).ToDictionary(keyValue => keyValue.Key, keyValue => keyValue.Value);

					if (recordMapping) {
						foreach (var kvp in symbols) {
							mappings[kvp.Value] = kvp.Key;
						}
						return Task.CompletedTask;
					}

					void AssertMapping(string symbolName) {
						Assert.True(mappings.TryGetValue(symbolName, out var obfuscatedSymbol));
						Assert.True(symbols.TryGetValue(obfuscatedSymbol, out var decryptedSymbol));
						Assert.Equal(symbolName, decryptedSymbol);
					}

					AssertMapping("MethodOverloading.Class");
					AssertMapping("MethodOverloading.Program/NestedClass");

					if (shortNames) {
						AssertMapping("OverloadedMethod");
						AssertMapping("Field");
						AssertMapping("Property");
						AssertMapping("Event");
					}
					else {
						AssertMapping("MethodOverloading.Program::OverloadedMethod(System.Object[])");
						AssertMapping("MethodOverloading.Program::OverloadedMethod(System.String)");
						AssertMapping("MethodOverloading.BaseClass::Field");
						AssertMapping("MethodOverloading.BaseClass::Property");
						AssertMapping("MethodOverloading.BaseClass::Event");
					}

					return Task.CompletedTask;
				}
			);

		public static IEnumerable<object[]> MethodOverloadingData() {
			foreach (var shortNames in new[] {false, true}) {
				foreach (var preserveGenericParams in new[] {false, true}) {
					yield return new object[] {shortNames, preserveGenericParams};
				}
			}
		}
	}
}
