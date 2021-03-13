using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using Confuser.Protections.Constants;
using Confuser.Core.Services;

namespace ConstantProtection.Test {
	public sealed class ConstantProtectionTest : TestBase {
		public ConstantProtectionTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		public static TheoryData<ConstantProtectionTestCase> ProtectAndExecuteTestData { get; } = BuildProtectAndExecuteTestData();

		[Theory]
		[MemberData(nameof(ProtectAndExecuteTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "constants")]
		public async Task ProtectAndExecuteTest(ConstantProtectionTestCase testData) {
			var baseDir = Path.Combine(Environment.CurrentDirectory, testData.Framework);
			var inputFile = Path.Combine(baseDir, "ConstantProtection.exe");

			var recordedResult = await ProcessUtilities.ExecuteTestApplication(inputFile, RecordOutput, outputHelper).ConfigureAwait(true);

			await Run(testData.Framework, 
				"ConstantProtection.exe", 
				recordedResult.Result, 
				testData.Apply(new SettingItem<IProtection>("constants")),
				outputDirSuffix: Guid.NewGuid().ToString());
		}

		private static async Task<string[]> RecordOutput(StreamReader reader) {
			var result = new List<string>();

			string line;
			while ((line = await reader.ReadLineAsync().ConfigureAwait(true)) != null) {
				result.Add(line);
			}

			Assert.Equal("START", result[0]);
			Assert.Equal("END", result[^1]);

			result.RemoveAt(0);
			result.RemoveAt(result.Count - 1);

			return result.ToArray();
		}

		internal static TheoryData<ConstantProtectionTestCase> BuildProtectAndExecuteTestData() => (
			from framework in new string[] { "net20", "net40", "net471" }
			from mode in Enum.GetValues<Mode>()
			from compressor in  Enum.GetValues<CompressionAlgorithm>()
			from cfg in new bool[] { false, true }
			from encodeStrings in new EncodeElements[] { EncodeElements.None, EncodeElements.Strings }
			from encodeNumbers in new EncodeElements[] { EncodeElements.None, EncodeElements.Numbers }
			from encodePrimitives in GetPrimitivesOptions(encodeStrings | encodeNumbers)
			from encodeInitializers in new EncodeElements[] { EncodeElements.None, EncodeElements.Initializers }
			select new ConstantProtectionTestCase() with { 
				Framework = framework,
				Mode = mode,
				Compressor = compressor,
				ControlFlowGraph = cfg,
				Elements = encodeStrings | encodeNumbers | encodePrimitives | encodeInitializers 
			}).Aggregate(new TheoryData<ConstantProtectionTestCase>(), (td, tc) => { td.Add(tc); return td; });

		private static EncodeElements[] GetPrimitivesOptions(EncodeElements encodeElements) {
			EncodeElements[] primitivesOptions;

			if ((encodeElements & (EncodeElements.Strings | EncodeElements.Numbers)) == 0)
				primitivesOptions = new EncodeElements[] { EncodeElements.None };
			else
				primitivesOptions = new EncodeElements[] { EncodeElements.None, EncodeElements.Primitive };

			return primitivesOptions;
		}
	}
}
