using System;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace SignatureMismatch2.Test {
	public class SignatureMismatch2Test {
		private readonly ITestOutputHelper outputHelper;

		public SignatureMismatch2Test(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/187")]
		public async Task SignatureMismatch2() =>
			await TestRunner.Run(
				new [] { "SignatureMismatch2.exe", "SignatureMismatch2Helper.dll" },
				new [] { "External" },
				new SettingItem<Protection>("rename") { ["renPublic"] = "true" },
				outputHelper
			);
	}
}
