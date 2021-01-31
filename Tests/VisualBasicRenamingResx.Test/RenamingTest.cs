using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace VisualBasicRenamingResx.Test {
	public sealed class RenamingTest : TestBase {
		public RenamingTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(ProtectAndExecuteTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/25")]
		public async Task ProtectAndExecuteTest(string framework) =>
			await Run(
				framework,
				"VisualBasicRenamingResx.exe",
				new[] {"Test (neutral)"},
				new SettingItem<IProtection>("rename"));

		public static IEnumerable<object[]> ProtectAndExecuteTestData() {
			foreach (var framework in new string[] { "net40", "net471" })
				yield return new object[] { framework };
		}
	}
}
