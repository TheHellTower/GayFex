using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

namespace WinFormsRenaming.Test {
	public class RenameDataPropertyNameTest : TestBase {
		public RenameDataPropertyNameTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(RenameWindowsFormsTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "Windows Forms")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/54")]
		public Task RenameWindowsFormsTest(string framework) => Run(
			framework,
			"WinFormsRenaming.dll",
			null,
			new SettingItem<IProtection>("rename"),
			outputAction: message => Assert.DoesNotContain("Failed to extract binding property name in", message));

		public static IEnumerable<object[]> RenameWindowsFormsTestData() =>
			from framework in "net35;net40;net48".Split(';')
			select new object[] { framework };
	}
}
