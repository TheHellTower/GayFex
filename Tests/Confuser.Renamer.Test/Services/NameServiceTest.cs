using System;
using Confuser.Core.Services;
using Confuser.Renamer.Services;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Renamer.Test.Services {
	public class NameServiceTest {
		private ITestOutputHelper outputHelper;

		public NameServiceTest(ITestOutputHelper outputHelper) => 
			this.outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", NameProtection._Id)]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/37")]
		public void TestDecodableGenericName() {
			var protection = new NameProtection();
			var logger = new XunitLogger(outputHelper);

			var serviceProvider = Mock.Of<IServiceProvider>();
			var randomService = new RandomService(string.Empty);
			var loggerService = Mock.Of<ILoggerFactory>();
			Mock.Get(serviceProvider).Setup(p => p.GetService(typeof(IRandomService))).Returns(randomService);
			Mock.Get(serviceProvider).Setup(p => p.GetService(typeof(ILoggerFactory))).Returns(loggerService);
			Mock.Get(loggerService).Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(logger);

			var nameService = new NameService(serviceProvider, protection);

			var methodName = "TestMethod";
			var obfuscatedName1 = nameService.ObfuscateName(methodName, RenameMode.Decodable);
			Assert.NotEmpty(obfuscatedName1);

			methodName += "`1";
			var obfuscatedName2 = nameService.ObfuscateName(methodName, RenameMode.Decodable);
			Assert.EndsWith("`1", obfuscatedName2, StringComparison.Ordinal);

			var obfuscatedName3 = nameService.ObfuscateName(methodName, RenameMode.Decodable);
			Assert.EndsWith("`1", obfuscatedName3, StringComparison.Ordinal);
			Assert.Equal(obfuscatedName2, obfuscatedName3, StringComparer.Ordinal);
		}
	}
}
