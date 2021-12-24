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
	public sealed class AntiDumpTest : AntiProtectionsTest {
		public AntiDumpTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(AntiDumpTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "anti dump")]
		public Task ProtectAntiDumpAndExecute(string framework) =>
			RunWithSettings(
				framework,
				new SettingItem<IProtection>(AntiDumpProtection._Id),
				$"_dump"
			);

		public static IEnumerable<object[]> AntiDumpTestData() {
			foreach (var framework in GetTargetFrameworks())
				yield return new object[] { framework };
		}
	}
}
