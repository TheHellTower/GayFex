using System;
using Confuser.Core.Services;
using Confuser.Renamer.Services;
using Confuser.UnitTest;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Renamer.Test.Services {
	public class NameServiceTest : IDisposable {
		private readonly ITestOutputHelper _outputHelper;
		private readonly XunitLogger _logger;

		public NameServiceTest(ITestOutputHelper outputHelper) {
			_outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));
			_logger = new XunitLogger(outputHelper);
		}

		[Fact]
		[Trait("Category", "Protection")]
		[Trait("Protection", NameProtection._Id)]
		[Trait("Issue", "https://github.com/mkaring/ConfuserEx/issues/37")]
		public void TestDecodableGenericName() {
			var protection = new NameProtection();

			var serviceProvider = Mock.Of<IServiceProvider>();
			var randomService = new RandomService(string.Empty);
			var loggerService = Mock.Of<ILoggerFactory>();
			Mock.Get(serviceProvider).Setup(p => p.GetService(typeof(IRandomService))).Returns(randomService);
			Mock.Get(serviceProvider).Setup(p => p.GetService(typeof(ILoggerFactory))).Returns(loggerService);
			Mock.Get(loggerService).Setup(p => p.CreateLogger(It.IsAny<string>())).Returns(_logger);

			var nameService = new NameService(serviceProvider, protection);

			var methodName = "TestMethod";
			var obfuscatedName1 = nameService.ObfuscateName(methodName, RenameMode.Decodable);
			Assert.NotEmpty(obfuscatedName1);

			methodName += "`1";
			var obfuscatedName2 = nameService.ObfuscateName(null, methodName, RenameMode.Decodable, true);
			Assert.EndsWith("`1", obfuscatedName2, StringComparison.Ordinal);

			var obfuscatedName3 = nameService.ObfuscateName(null, methodName, RenameMode.Decodable, true);
			Assert.EndsWith("`1", obfuscatedName3, StringComparison.Ordinal);
			Assert.Equal(obfuscatedName2, obfuscatedName3, StringComparer.Ordinal);
		}

		#region IDisposable Support
		private bool _disposed = false;

		protected virtual void Dispose(bool disposing) {
			if (!_disposed) {
				if (disposing) {
					_logger.Dispose();
				}
				_disposed = true;
			}
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
