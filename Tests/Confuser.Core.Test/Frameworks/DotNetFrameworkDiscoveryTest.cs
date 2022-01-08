namespace Confuser.Core.Frameworks {
	public class DotNetFrameworkDiscoveryTest : DiscoveryTestBase {
		protected override IFrameworkDiscovery Discovery { get; } = new DotNetFrameworkDiscovery();
	}
}
