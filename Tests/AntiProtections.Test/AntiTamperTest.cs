using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Protections;
using Confuser.Protections.AntiTamper;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public sealed class AntiTamperTest : AntiProtectionsTest {
		public AntiTamperTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(AntiTamperTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti tamper")]
		public Task ProtectAntiTamperAndExecute(string framework, AntiTamperMode antiTamperMode, KeyDeriverMode keyDeriverMode) =>
			RunWithSettings(
				framework,
				new SettingItem<IProtection>(AntiTamperProtection._Id) {
					{ "mode", Enum.GetName(antiTamperMode) },
					{ "key", Enum.GetName(keyDeriverMode) }
				},
				$"_tamper_{antiTamperMode}_{keyDeriverMode}"
			);

		public static IEnumerable<object[]> AntiTamperTestData() {
			foreach (var framework in GetTargetFrameworks())
				foreach (var mode in Enum.GetValues<AntiTamperMode>())
					foreach (var deriver in Enum.GetValues<KeyDeriverMode>())
						yield return new object[] { framework, mode, deriver };
		}
	}
}
