using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace SignatureMismatch2.Test {
	public class SignatureMismatch2Test : TestBase {
		public SignatureMismatch2Test(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(SignatureMismatch2Data))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/187")]
		public async Task SignatureMismatch2(string framework) =>
			await Run(
				framework,
				new [] { "SignatureMismatch2.exe", "SignatureMismatch2Helper.dll" },
				new [] { "External", "External" },
				new SettingItem<IProtection>("rename") { ["renPublic"] = "true" }
			);

		public static IEnumerable<object[]> SignatureMismatch2Data() {
			foreach (var framework in "net20;net40;net471".Split(';'))
				yield return new object[] { framework };
		}
	}
}
