using Xunit;

namespace Confuser.Core.Frameworks {
	public abstract class DiscoveryTestBase {
		protected abstract IFrameworkDiscovery Discovery { get; }

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "framework discovery")]
		public void CreateAssemblyResolver() {
			Assert.NotEmpty(Discovery.GetInstalledFrameworks());
		}

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "framework discovery")]
		public void DiscoverDotNetFramework() {
			foreach (var framework in Discovery.GetInstalledFrameworks()) {
				Assert.NotNull(framework);
				Assert.NotNull(framework.CreateAssemblyResolver());
			}
		}
	}
}