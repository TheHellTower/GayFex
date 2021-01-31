using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace SignatureMismatch.Test {
	public class SignatureMismatchTest : TestBase {
		public SignatureMismatchTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(SignatureMismatchData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/78")]
		public async Task SignatureMismatch(string framework) =>
			await Run(
				framework,
				"78_SignatureMismatch.exe",
				new [] {
					"Dictionary created",
					"Dictionary count: 1",
					"[Test1] = Test2"
				},
				new SettingItem<IProtection>("rename")
			);

		public static IEnumerable<object[]> SignatureMismatchData() {
			foreach (var framework in "net20;net40;net471".Split(';'))
				yield return new object[] { framework };
		}
	}
}
