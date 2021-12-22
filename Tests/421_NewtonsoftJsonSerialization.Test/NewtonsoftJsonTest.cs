using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace NewtonsoftJsonSerialization.Test {
	public class NewtonsoftJsonTest : TestBase {
		public NewtonsoftJsonTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(NewtonsoftJsonAttributeDetectionData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/421")]
		public async Task NewtonsoftJsonAttributeDetection(string framework) =>
			await Run(
				framework,
				new[] {
					"421_NewtonsoftJsonSerialization.exe",
					"external:Newtonsoft.Json.dll"
				},
				new [] {
					"{\"a\":\"a\",\"b\":\"b\",\"c\":\"c\"}",
					"{\"a\":\"a\",\"b\":\"b\",\"c\":\"c\"}"
				},
				new SettingItem<IProtection>("rename")
			);

		public static IEnumerable<object[]> NewtonsoftJsonAttributeDetectionData() {
			foreach (var framework in "net35;net40;net471".Split(';'))
				yield return new object[] { framework };
		}
	}
}
