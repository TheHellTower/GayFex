using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using Confuser.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConfuserEx {
	internal static class ComponentDiscovery {
		public static void LoadComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers) {
			var container = PluginDiscovery.Instance.GetPlugins(NullLogger.Instance);
			LoadComponents(protections, packers, container);
		}

		public static void LoadComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers, string plugInPath) => 
			LoadComponents(protections, packers, ImmutableArray.Create(plugInPath));

		public static void LoadComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers, IEnumerable<string> plugInPaths) {
			var container = PluginDiscovery.Instance.GetPlugins(plugInPaths, NullLogger.Instance);
			LoadComponents(protections, packers, container);
		}

		private static void LoadComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers, ExportProvider exports) {
			foreach (var exportedProtection in exports.GetExports<IProtection, IProtectionMetadata>()) {
				var newComponent = new ConfuserUiComponent(exportedProtection);
				if (!protections.Contains(newComponent))
					protections.Add(newComponent);
			}

			foreach (var exportedPacker in exports.GetExports<IPacker, IPackerMetadata>())  {
				var newComponent = new ConfuserUiComponent(exportedPacker);
				if (!packers.Contains(newComponent))
					packers.Add(newComponent);
			}
		}

		public static void RemoveComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers,
			string pluginPath) {
			protections.RemoveWhere(comp =>
				String.Equals(comp.PlugInPath, pluginPath, StringComparison.OrdinalIgnoreCase));
			packers.RemoveWhere(comp => String.Equals(comp.PlugInPath, pluginPath, StringComparison.OrdinalIgnoreCase));
		}
	}
}
