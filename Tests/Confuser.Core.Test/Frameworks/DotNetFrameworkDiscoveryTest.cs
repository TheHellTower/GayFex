using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Confuser.Core.Frameworks {
	public class DotNetFrameworkDiscoveryTest {
		private DotNetFrameworkDiscovery Discovery { get; } = new DotNetFrameworkDiscovery();

		[Fact]
		[Trait("Category", "Core")]
		[Trait("Core", "framework discovery")]
		public void DiscoverDotNetFramework() {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

			Assert.NotEmpty(Discovery.GetInstalledFrameworks());
		}
	}
}
