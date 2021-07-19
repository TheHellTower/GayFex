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

					void CheckName(string expectedFullName, string expectedShortName, string obfuscatedName) {
						var fullName = deobfuscator.DeobfuscateSymbol(obfuscatedName, false);
						Assert.Equal(expectedFullName, fullName);
						Assert.Equal(expectedShortName, MessageDeobfuscator.ExtractShortName(fullName));
					}

					CheckName("MessageDeobfuscation.Class", "MessageDeobfuscation.Class",
						classId);
					CheckName("MessageDeobfuscation.Class/NestedClass", "NestedClass",
						nestedClassId);
					CheckName("MessageDeobfuscation.Class::Method(System.String,System.Int32)", "Method",
						methodId);
					CheckName("MessageDeobfuscation.Class::Field", "Field",
						fieldId);
					CheckName("MessageDeobfuscation.Class::Property", "Property",
						propertyId);
					CheckName("MessageDeobfuscation.Class::Event", "Event",
						eventId);

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
				"   at oQmpV$y2k2b9P3d6GP1cxGPuRtKaNIZvZcKpZXSfKFG8.CE8t0VDPQk9$jgv1XuRwt1k.FhsPrCLqIAaPKe7abGklvY4(String )",
				"   at EbUjRcrC76NnA7RJlhQffrfp$vMGHdDfqtVFtWrAOPyD.xgIw9voebB21PlxPFA_hs60()"
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

					void CheckName(string expectedName, string obfuscatedName) {
						var name = deobfuscator.DeobfuscateSymbol(obfuscatedName, true);
						Assert.Equal(expectedName, name);
					}

					CheckName("MessageDeobfuscation.Class", "oQmpV$y2k2b9P3d6GP1cxGPuRtKaNIZvZcKpZXSfKFG8");
					CheckName("NestedClass", "CE8t0VDPQk9$jgv1XuRwt1k");
					CheckName("Method", "jevJU4p4yNrAYGqN7GkRWaI");
					CheckName("Field", "3IS4xsnUsvDQZop6e4WmNVw");
					CheckName("Property", "917VMBMNYHd0kfnnNkgeJ10");
					CheckName("Event", "AIyINk7kgFLFc73Md8Nu8Z0");

					Assert.Equal(_expectedDeobfuscatedOutput, deobfuscatedMessage);
					return Task.Delay(0);
				}
			);
		}
	}
}
