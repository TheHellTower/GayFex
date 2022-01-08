using Xunit.Abstractions;

namespace Confuser.Core.Frameworks {
	public class DotNetDiscoveryTest : DiscoveryTestBase {
		public DotNetDiscoveryTest(ITestOutputHelper outputHelper) : base(outputHelper) {}

		protected override IFrameworkDiscovery Discovery { get; } = new DotNetDiscovery();
	}
}
