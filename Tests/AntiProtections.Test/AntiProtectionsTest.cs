using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace AntiProtections.Test {
	public class AntiProtectionsTest : TestBase {
		public AntiProtectionsTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		protected Task RunWithSettings(string framework, SettingItem<IProtection> settingItem, string outputSuffix) =>
			Run(
				framework,
				GetExecutableName(framework),
				new[] {
				  "This is a test."
				},
				settingItem,
				outputSuffix,
				projectModuleAction: m => {
					m.SNKeyPath = GetKeyFile();
				}
			);

		protected static string GetExecutableName(string targetFramework) =>
			targetFramework.StartsWith("netstandard") || targetFramework.StartsWith("netcoreapp") ? "AntiProtections.dll" : "AntiProtections.exe";

		protected static string GetKeyFile() {
			var key = Path.Combine(Environment.CurrentDirectory, "Confuser.Test.snk");
			Assert.True(File.Exists(key));
			return key;
		}

		protected static IEnumerable<string> GetTargetFrameworks() => new string[] { "net20", "net40", "net471" };
	}
}
