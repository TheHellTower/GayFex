using System;
using System.IO;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Core.Project;
using Confuser.Renamer;
using Confuser.UnitTest;
using Xunit;
using Xunit.Abstractions;

namespace MessageDeobfuscation.Test {
	public class MessageDeobfuscationTest : TestBase {
		readonly string _expectedDeobfuscatedOutput = String.Join(Environment.NewLine,
			"Exception",
			"   at MessageDeobfuscation.Class.NestedClass.Method(String )",
			"   at MessageDeobfuscation.Program.Main()");

		public MessageDeobfuscationTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		public async Task MessageDeobfuscationWithSymbolsMap() {
			var expectedObfuscatedOutput = new[] {
				"Exception",
				"   at _OokpKOmal5JNZMPvSAFgHLHjBke._tc5CFDIJ2J9Fx3ehd3sgjTMAxCaA._8Tq88jpv7mEXkEMavg6AaMFsXJt(String )",
				"   at _ykdLsBmsKGrd6fxeEseqJs8XlpP._tfvbqapfg44suL8taZVvOKM4AoG()"
			};
			await Run(
				"MessageDeobfuscation.exe",
				expectedObfuscatedOutput,
				new SettingItem<Protection>("rename") {["mode"] = "decodable"},
				"SymbolsMap",
				seed: "1234",
				postProcessAction: outputPath => {
					var messageDeobfuscator = MessageDeobfuscator.Load(Path.Combine(outputPath, "symbols.map"));
					var deobfuscated = messageDeobfuscator.Deobfuscate(string.Join(Environment.NewLine, expectedObfuscatedOutput));
					Assert.Equal(_expectedDeobfuscatedOutput, deobfuscated);
					return Task.Delay(0);
				}
			);
		}

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		public async Task MessageDeobfuscationWithPassword() {
			var expectedObfuscatedOutput = new[] {
				"Exception",
				"   at oQmpV$y2k2b9P3d6GP1cxGPuRtKaNIZvZcKpZXSfKFG8.V1M$X52eDxP6ElgdFrRDlF0KSZU31AmQaiXXgzyoeJJ4KV64JBpi0Bh25Xdje$vCxw.fUHV$KyBiFTUH0$GNDHVx6XvtlZWHnzVgRO9N2M$jw5ysYWJWaUSMQYtPDT$wa$6MarZQoNxnbR_9cn$A2XXvRY(String )",
				"   at EbUjRcrC76NnA7RJlhQffrfp$vMGHdDfqtVFtWrAOPyD.swzvaIVl3W8yDi8Ii3P1j_V9JC8eVu2JgvNNjeVDYc4bOHH37cCBf0_3URE_8UcWPQ()"
			};
			string password = "password";
			await Run(
				"MessageDeobfuscation.exe",
				expectedObfuscatedOutput,
				new SettingItem<Protection>("rename") {["mode"] = "reversible", ["password"] = password},
				"Password",
				postProcessAction: outputPath => {
					var messageDeobfuscator = new MessageDeobfuscator(password);
					var deobfuscated = messageDeobfuscator.Deobfuscate(string.Join(Environment.NewLine, expectedObfuscatedOutput));
					Assert.Equal(_expectedDeobfuscatedOutput, deobfuscated);
					return Task.Delay(0);
				}
			);
		}
	}
}
