using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Renamer;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace _123_InheritCustomAttr.Test {
	public class InheritCustomAttributeTest : TestBase {
		public InheritCustomAttributeTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(InheritCustomAttributeData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/123")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/161")]
		public async Task InheritCustomAttribute(string framework, string renameMode, bool flatten) =>
			await Run(
				framework,
				"123_InheritCustomAttr.exe",
				new[] {"Monday", "43", "1"},
				new SettingItem<IProtection>("rename") {{"mode", renameMode}, {"flatten", flatten ? "True" : "False"}},
				$"_{renameMode}_{flatten}",
				l => Assert.False(l.StartsWith("[WARN]"), "Logged line may not start with [WARN]\r\n" + l));

		public static IEnumerable<object[]> InheritCustomAttributeData() {
			foreach (var framework in "net20;net40;net471".Split(';'))
				foreach (var renameMode in new [] { nameof(RenameMode.Unicode), nameof(RenameMode.ASCII), nameof(RenameMode.Letters), nameof(RenameMode.Debug), nameof(RenameMode.Retain) })
					foreach (var flatten in new [] { true, false })
						yield return new object[] { framework, renameMode, flatten };
		}
	}
}
