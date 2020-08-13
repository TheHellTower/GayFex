using System;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace WpfRenaming.Test {
	public class ProcessWpfTest {
		private readonly ITestOutputHelper outputHelper;

		public ProcessWpfTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		/// <see cref="https://github.com/mkaring/ConfuserEx/issues/1"/>
		[Fact]
		[Trait("Category", "Analysis")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "WPF")]
		public async Task ProcessWithoutObfuscationTest() =>
			await TestRunner.Run(
				"WpfRenaming.dll",
				null,
				null,
				outputHelper);

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "WPF")]
		public async Task ProcessWithObfuscationTest() =>
			await TestRunner.Run(
				"WpfRenaming.dll",
				null,
				new SettingItem<Protection>("rename"),
				outputHelper);
	}
}
