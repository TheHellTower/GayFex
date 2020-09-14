using System.Threading.Tasks;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace IncorrectRedirectToGac.Test {
	public class IncorrectRedirectToGacTest : TestBase {
		public IncorrectRedirectToGacTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact (Skip = "TODO: https://github.com/mkaring/ConfuserEx/issues/144")]
		[Trait("Category", "core")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/144")]
		public async Task IncorrectRedirectToGac() =>
			await Run(
				new [] { "IncorrectRedirectToGac.exe", "Microsoft.Build.Framework.dll" }, new string[0], null
			);
	}
}
