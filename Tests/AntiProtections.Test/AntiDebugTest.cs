using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Protections;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public sealed class AntiDebugTest : AntiProtectionsTest {
		public AntiDebugTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(AntiDebugTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti debug")]
		public Task ProtectAntiDebugAndExecute(string framework, AntiDebugMode antiDebugMode) =>
			RunWithSettings(
				framework,
				new SettingItem<IProtection>(AntiDebugProtection._Id) {
					{ "mode", Enum.GetName(antiDebugMode) }
				},
				$"_dump_{antiDebugMode}"
			);

		public static IEnumerable<object[]> AntiDebugTestData() {
			foreach (var framework in GetTargetFrameworks())
				foreach (var mode in Enum.GetValues<AntiDebugMode>())
					yield return new object[] { framework, mode };
		}
	}
}
