using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace ConstantsInlining.Test {
	public class ConstantInliningTest : TestBase {
		public ConstantInliningTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(ConstantInliningData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "constants")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/193")]
		public async Task ConstantInlining(string framework) =>
			await Run(
				framework,
				new[] { "193_ConstantsInlining.exe", "193_ConstantsInlining.Lib.dll" },
				new[] { "From External" },
				new SettingItem<IProtection>("constants") { { "elements", "S" } });

		public static IEnumerable<object[]> ConstantInliningData() {
			foreach (var framework in "net20;net40;net471".Split(';'))
				yield return new object[] { framework };
		}
	}
}
