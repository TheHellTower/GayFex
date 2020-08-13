using System;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiTamper.Test {
	public sealed class AntiTamperTest {
		private readonly ITestOutputHelper outputHelper;

		public AntiTamperTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[InlineData("normal")]
		[InlineData("anti")]
		[InlineData("jit", Skip = "Runtime Component of the JIT AntiTamper protection is broken.")]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti tamper")]
		public async Task ProtectAntiTamperAndExecute(string antiTamperMode) =>
			await TestRunner.Run("AntiTamper.exe",
				new[] {"This is a test."},
				new SettingItem<Protection>("anti tamper") {{"mode", antiTamperMode}},
				outputHelper);
	}
}
