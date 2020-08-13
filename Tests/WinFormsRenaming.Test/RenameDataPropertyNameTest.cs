using System;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace WinFormsRenaming.Test {
	public class RenameDataPropertyNameTest {
		private readonly ITestOutputHelper outputHelper;

		public RenameDataPropertyNameTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "Windows Forms")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/54")]
		public async Task RenameWindowsFormsTest() =>
			await TestRunner.Run(
				"WinFormsRenaming.dll",
				null,
				new SettingItem<Protection>("rename"),
				outputHelper,
				outputAction: message => Assert.DoesNotContain("Failed to extract binding property name in", message));
	}
}
