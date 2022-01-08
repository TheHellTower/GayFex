using Xunit.Abstractions;

namespace Confuser.Core.Frameworks {
	public class DotNetFrameworkDiscoveryTest : DiscoveryTestBase {
		public DotNetFrameworkDiscoveryTest(ITestOutputHelper outputHelper) : base(outputHelper) { }

		protected override IFrameworkDiscovery Discovery { get; } = new DotNetFrameworkDiscovery();
	}
}
