using System;
using System.Collections.Generic;
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

		[Theory]
		[MemberData(nameof(RenameModeAndExpectedObfuscatedOutput))]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		public async Task MessageDeobfuscationWithSymbolsMap(string renameMode, string[] expectedObfuscatedOutput) =>
			await Run(
				"MessageDeobfuscation.exe",
				expectedObfuscatedOutput,
				new SettingItem<Protection>("rename") { ["mode"] = renameMode },
				$"SymbolsMap_{renameMode}",
				seed: "1234",
				postProcessAction: outputPath => {
					var deobfuscator = MessageDeobfuscator.Load(Path.Combine(outputPath, "symbols.map"));
					var deobfuscatedMessage =
						deobfuscator.DeobfuscateMessage(string.Join(Environment.NewLine, expectedObfuscatedOutput));

					string classId, nestedClassId, methodId, fieldId, propertyId, eventId;
					if (renameMode == nameof(RenameMode.Decodable)) {
						classId = "_OokpKOmal5JNZMPvSAFgHLHjBke";
						nestedClassId = "_tc5CFDIJ2J9Fx3ehd3sgjTMAxCaA";
						methodId = "_zbgDV4jbK6Oi9WBq66uG2ct7IoRA";
						fieldId = "_QHxqC1xaBFmUQawCZSOQpattICo";
						propertyId = "_FJthtfOBOiQFgVDIymbi3wwJoeN";
						eventId = "_cbPBZqkDuaNXOkmJtacrG2uYfZs";
					}
					else {
						classId = "_g";
						nestedClassId = "_e";
						methodId = "_C";
						fieldId = "_d";
						propertyId = "_E";
						eventId = "_b";
					}

					CheckName("MessageDeobfuscation.Class", "MessageDeobfuscation.Class",
						classId, deobfuscator);
					CheckName("MessageDeobfuscation.Class/NestedClass", "NestedClass",
						nestedClassId, deobfuscator);
					CheckName("MessageDeobfuscation.Class::Method(System.String,System.Int32)", "Method",
						methodId, deobfuscator);
					CheckName("MessageDeobfuscation.Class::Field", "Field",
						fieldId, deobfuscator);
					CheckName("MessageDeobfuscation.Class::Property", "Property",
						propertyId, deobfuscator);
					CheckName("MessageDeobfuscation.Class::Event", "Event",
						eventId, deobfuscator);

					Assert.Equal(_expectedDeobfuscatedOutput, deobfuscatedMessage);
					return Task.Delay(0);
				}
			);

		public static IEnumerable<object[]> RenameModeAndExpectedObfuscatedOutput() =>
			new[] {
				new object[] {
					nameof(RenameMode.Decodable),
					new[] {
						"Exception",
						"   at _OokpKOmal5JNZMPvSAFgHLHjBke._tc5CFDIJ2J9Fx3ehd3sgjTMAxCaA._8Tq88jpv7mEXkEMavg6AaMFsXJt(String )",
						"   at _ykdLsBmsKGrd6fxeEseqJs8XlpP._tfvbqapfg44suL8taZVvOKM4AoG()"
					}
				},
				new object[] {
					nameof(RenameMode.Sequential),
					new[] {
						"Exception",
						"   at _g._e._c(String )",
						"   at _B._f()"
					}
				}
			};

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
				new SettingItem<Protection>("rename") {
					["mode"] = "reversible",
					["password"] = password,
					["renPdb"] = "true"
				},
				"Password",
				postProcessAction: outputPath => {
					var deobfuscator = new MessageDeobfuscator(password);
					var deobfuscatedMessage = deobfuscator.DeobfuscateMessage(string.Join(Environment.NewLine, expectedObfuscatedOutput));

					CheckName("MessageDeobfuscation.Class", "MessageDeobfuscation.Class",
						"oQmpV$y2k2b9P3d6GP1cxGPuRtKaNIZvZcKpZXSfKFG8", deobfuscator);
					CheckName("MessageDeobfuscation.Class/NestedClass", "NestedClass",
						"V1M$X52eDxP6ElgdFrRDlF0KSZU31AmQaiXXgzyoeJJ4KV64JBpi0Bh25Xdje$vCxw", deobfuscator);
					CheckName("MessageDeobfuscation.Class::Method(System.String,System.Int32)", "Method",
						"CPiRF0I_h5xVXKPEtJXNA7cLoPPS4vhkcjcJi6MAreEi2dBd0rRGyabz9ko1cgWS46oQIMTt_U99FxMd$wpcMBI", deobfuscator);
					CheckName("MessageDeobfuscation.Class::Field", "Field",
						"EbUjRcrC76NnA7RJlhQffrdNtUhGQ3K5irENJz724HX_R45xF8Tm$vzXOkAiVX4bXA", deobfuscator);
					CheckName("MessageDeobfuscation.Class::Property", "Property",
						"jpG4Jhvg51oUy7PG8hUhxDmJkR1IVttcUFtkOHedcZ2BvCUAjb2SvUsd3q9IoA2LEQ", deobfuscator);
					CheckName("MessageDeobfuscation.Class::Event", "Event",
						"NdAzNJfOt9g8GfsT5YEikaIAKenWzJC2RbWKmG8rcYU4f2t_KXIZp4wSkiAmLQe8sA", deobfuscator);

					Assert.Equal(_expectedDeobfuscatedOutput, deobfuscatedMessage);
					return Task.Delay(0);
				}
			);
		}

		void CheckName(string expectedFullName, string expectedShortName, string obfuscatedName, MessageDeobfuscator messageDeobfuscator) {
			var fullName = messageDeobfuscator.DeobfuscateSymbol(obfuscatedName, false);
			Assert.Equal(expectedFullName, fullName);
			Assert.Equal(expectedShortName, MessageDeobfuscator.ExtractShortName(fullName));
		}
	}
}
