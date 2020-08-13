using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Renamer;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace DynamicTypeRename.Test {
	public class RenameDynamicTypeTest {
		private readonly ITestOutputHelper outputHelper;

		public RenameDynamicTypeTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Theory]
		[MemberData(nameof(RenameDynamicTypeData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/161")]
		public async Task RenameDynamicType(string renameMode, bool flatten) =>
			await TestRunner.Run(
				"161_DynamicTypeRename.exe",
				new [] {
					"Type declaration done",
					"Dynamic type created",
					"Fields in type: 1",
					"Fetching field value is okay"
				},
				new SettingItem<Protection>("rename") {
					{ "mode", renameMode },
					{ "flatten", flatten.ToString() }
				},
				outputHelper,
				"testtmp_" + Guid.NewGuid()
			);

		public static IEnumerable<object[]> RenameDynamicTypeData() {
			foreach (var renameMode in new [] { nameof(RenameMode.Unicode), nameof(RenameMode.ASCII), nameof(RenameMode.Letters), nameof(RenameMode.Debug), nameof(RenameMode.Retain) })
				foreach (var flatten in new [] { true, false })
					yield return new object[] { renameMode, flatten };
		}
	}
}
