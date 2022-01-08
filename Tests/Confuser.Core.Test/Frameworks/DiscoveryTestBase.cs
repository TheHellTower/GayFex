using System;
using Confuser.UnitTest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Confuser.Core.Frameworks {
	public abstract class DiscoveryTestBase {
		protected abstract IFrameworkDiscovery Discovery { get; }

		private readonly ITestOutputHelper _outputHelper;

		public DiscoveryTestBase(ITestOutputHelper outputHelper) =>
			_outputHelper = outputHelper ?? throw new ArgumentNullException(nameof(outputHelper));

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "framework discovery")]
		public void CreateAssemblyResolver() {
			Assert.NotEmpty(Discovery.GetInstalledFrameworks(CreateServiceProvider()));
		}

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "framework discovery")]
		public void DiscoverDotNetFramework() {
			foreach (var framework in Discovery.GetInstalledFrameworks(CreateServiceProvider())) {
				Assert.NotNull(framework);
				Assert.NotNull(framework.CreateAssemblyResolver());
			}
		}

		private IServiceProvider CreateServiceProvider() {
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddLogging(b => b.AddProvider(new XunitLogger(_outputHelper)));
			return serviceCollection.BuildServiceProvider();
		}
	}
}