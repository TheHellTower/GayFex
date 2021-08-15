using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace BlockingReferences.Test {
	public class BlockingReferencesTest : TestBase {
		public BlockingReferencesTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/379")]
		public async Task BlockingReferences() =>
			await Run(
				new [] { "BlockingReferences.exe", "BlockingReferencesHelper.dll" },
				new [] {
					"",
					"Implementation2",
				},
				new SettingItem<Protection>("rename") { ["renPublic"] = "true", ["mode"] = "decodable" }
			);
	}
}
