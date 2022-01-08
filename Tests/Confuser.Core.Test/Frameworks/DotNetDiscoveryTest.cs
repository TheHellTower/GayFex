namespace Confuser.Core.Frameworks {
	public class DotNetDiscoveryTest : DiscoveryTestBase {
		protected override IFrameworkDiscovery Discovery { get; } = new DotNetDiscovery();
	}
}
