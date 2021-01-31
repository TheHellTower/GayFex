using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Renamer;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DynamicTypeRename.Test {
	public class RenameDynamicTypeTest : TestBase {
		public RenameDynamicTypeTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Theory]
		[MemberData(nameof(RenameDynamicTypeData))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/161")]
		public Task RenameDynamicType(string framework, string renameMode, bool flatten) =>
			Run(
				framework,
				"161_DynamicTypeRename.exe",
				new [] {
					"Type declaration done",
					"Dynamic type created",
					"Fields in type: 1",
					"Fetching field value is okay"
				},
				new SettingItem<IProtection>("rename") {
					{ "mode", renameMode },
					{ "flatten", flatten.ToString() }
				},
				$"_{renameMode}_{flatten}"
			);

		public static IEnumerable<object[]> RenameDynamicTypeData() {
			foreach (var framework in "net20;net40;net471".Split(';'))
				foreach (var renameMode in new [] { nameof(RenameMode.Unicode), nameof(RenameMode.ASCII), nameof(RenameMode.Letters), nameof(RenameMode.Debug), nameof(RenameMode.Retain) })
					foreach (var flatten in new [] { true, false })
						yield return new object[] { framework, renameMode, flatten };
		}
	}
}
