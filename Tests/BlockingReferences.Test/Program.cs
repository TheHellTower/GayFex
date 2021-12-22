using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Renamer;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace BlockingReferences.Test {
	public class BlockingReferencesTest : TestBase {
		public BlockingReferencesTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(BlockingReferencesData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/379")]
		public async Task BlockingReferences(string framework, RenameMode renameMode) =>
			await BlockingReferencesInternal(framework, renameMode, false, "BlockingReferences.exe", "BlockingReferencesHelper.dll");

		[Theory]
		[MemberData(nameof(BlockingReferencesData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/379")]
		public async Task BlockingReferencesReverse(string framework, RenameMode renameMode) =>
			await BlockingReferencesInternal(framework, renameMode, true, "BlockingReferencesHelper.dll", "BlockingReferences.exe");

		private async Task BlockingReferencesInternal(string framework, RenameMode renameMode, bool reversed, params string[] files) =>
			await Run(
				framework,
				files,
				new[] {
					"",
					"Implementation2",
				},
				new SettingItem<IProtection>("rename") {
					["renPublic"] = "true",
					["mode"] = Enum.GetName(renameMode)
				},
				$"_{(reversed ? "reversed_" : "")}{renameMode}",
				outputAction: line => {
					Assert.DoesNotContain("[WARN] Failed to rename all targeted members", line);
				}
			);

		public static IEnumerable<object[]> BlockingReferencesData() {
			foreach (var framework in "net35;net40;net471".Split(';'))
				foreach (RenameMode renameMode in Enum.GetValues<RenameMode>().Except(new []{ RenameMode.Empty }) )
					yield return new object[] { framework, renameMode };
		}
	}
}
