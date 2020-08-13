using System;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace VisualBasicRenamingResx.Test {
	public sealed class RenamingTest {
		private readonly ITestOutputHelper outputHelper;

		public RenamingTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/25")]
		public async Task ProtectAndExecuteTest() =>
			await TestRunner.Run("VisualBasicRenamingResx.exe",
				new[] {"Test (neutral)"},
				new SettingItem<Protection>("rename"),
				outputHelper);
	}
}
