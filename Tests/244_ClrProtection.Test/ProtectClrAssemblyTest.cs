using System;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace ClrProtection.Test {
	public class ProtectClrAssemblyTest : TestBase {
		public ProtectClrAssemblyTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti tamper")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/244")]
		public Task AntiTamperProtection() => Run(
			"244_ClrProtection.exe",
			Array.Empty<String>(),
			new SettingItem<IProtection>("anti tamper"),
			$"_{nameof(AntiTamperProtection)}");

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "resources")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/244")]
		public Task ResourceProtection() => Run(
			"244_ClrProtection.exe",
			Array.Empty<String>(),
			new SettingItem<IProtection>("resources"),
			$"_{nameof(ResourceProtection)}");

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "typescramble")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/244")]
		public Task TypeScrambleProtection() => Run(
			"244_ClrProtection.exe",
			Array.Empty<String>(),
			new SettingItem<IProtection>("typescramble"),
			$"_{nameof(TypeScrambleProtection)}");

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti tamper")]
		[Trait("Protection", "resources")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/244")]
		public Task AntiTamperResourceProtection() => Run(
			"244_ClrProtection.exe",
			Array.Empty<String>(),
			new[] {new SettingItem<IProtection>("anti tamper"), new SettingItem<IProtection>("resources") },
			$"_{nameof(AntiTamperResourceProtection)}");
	}
}
