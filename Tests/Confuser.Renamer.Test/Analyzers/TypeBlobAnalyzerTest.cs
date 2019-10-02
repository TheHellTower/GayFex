using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Confuser.Core;
using Confuser.Renamer.Analyzers;
using Confuser.Renamer.Services;
using Confuser.UnitTest;
using dnlib.DotNet;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Renamer.Test.Analyzers {
	[Implementation(Value = 5)]
	public class TypeBlobAnalyzerTest {
		private readonly ITestOutputHelper outputHelper;

		public TypeBlobAnalyzerTest(ITestOutputHelper outputHelper) =>
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", "rename")]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/84")]
		public void AnalyseAttributeTest() {
			var moduleDef = Helpers.LoadTestModuleDef();

			var context = Mock.Of<IConfuserContext>();
			var nameService = Mock.Of<INameService>();
			Mock.Get(context).Setup(c => c.Modules).Returns(ImmutableArray.Create(moduleDef));

			void VerifyLog(string message) {
				Assert.DoesNotContain("Failed to resolve CA field", message, StringComparison.InvariantCulture);
				Assert.DoesNotContain("Failed to resolve CA property", message, StringComparison.InvariantCulture);
			}

			using (var logger = new XunitLogger(outputHelper, VerifyLog)) {
				TypeBlobAnalyzer.Analyze(context, nameService, logger, moduleDef);
			}

			Mock.Get(nameService).VerifyAll();
		}
	}
}
