using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

namespace WpfRenaming.Test {
	public class ProcessWpfTest : TestBase {
		public ProcessWpfTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		/// <see cref="https://github.com/mkaring/ConfuserEx/issues/1"/>
		[Theory]
		[MemberData(nameof(ProcessWithoutObfuscationTestData))]
		[Trait("Category", "Analysis")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "WPF")]
		public Task ProcessWithoutObfuscationTest(string framework) => Run(
			framework,
			"WpfRenaming.dll",
			null,
			NoProtections);

		[Theory]
		[MemberData(nameof(ProcessWithoutObfuscationTestData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Technology", "WPF")]
		public Task ProcessWithObfuscationTest(string framework) => Run(
			framework,
			"WpfRenaming.dll",
			null,
			new SettingItem<IProtection>("rename"));

		public static IEnumerable<object[]> ProcessWithoutObfuscationTestData() =>
			from framework in "net40;net48".Split(';')
			select new object[] { framework };
	}
}
