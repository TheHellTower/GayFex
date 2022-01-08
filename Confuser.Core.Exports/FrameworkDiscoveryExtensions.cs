using System;
using System.Collections.Generic;
using System.Text;

namespace Confuser.Core {
	public static class FrameworkDiscoveryExtensions {
		public static IEnumerable<IInstalledFramework> GetInstalledFrameworks(this IFrameworkDiscovery frameworkDiscovery, IConfuserContext context) {
			if (frameworkDiscovery is null) throw new ArgumentNullException(nameof(frameworkDiscovery));
			if (context is null) throw new ArgumentNullException(nameof(context));

			return frameworkDiscovery.GetInstalledFrameworks(context.Registry);
		}
	}
}
