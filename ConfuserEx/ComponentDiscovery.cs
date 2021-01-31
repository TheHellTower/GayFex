using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Runtime.Loader;
using Confuser.Core;

namespace ConfuserEx {
	internal class ComponentDiscovery {
		public static void LoadComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers, string pluginPath) {
			var loadContext = new AssemblyLoadContext("ctx", true);

			try {
				var assembly = loadContext.LoadFromAssemblyPath(pluginPath);
				var catalog = new AssemblyCatalog(assembly);
				var container = new CompositionContainer(catalog);
				foreach (var protection in container.GetExports<IProtection, IProtectionMetadata>()) 
					AddDistinct(protections, new ConfuserUiComponent(protection, pluginPath));

				foreach (var packer in container.GetExports<IPacker, IPackerMetadata>()) 
					AddDistinct(packers, new ConfuserUiComponent(packer, pluginPath));
			}
			finally {
				loadContext.Unload();
			}
		}

		private static void AddDistinct<T>(ICollection<T> list, T obj) {
			if (!list.Contains(obj))
				list.Add(obj);
		}

		public static void RemoveComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers,
			string pluginPath) {
			protections.RemoveWhere(comp =>
				String.Equals(comp.PlugInPath, pluginPath, StringComparison.OrdinalIgnoreCase));
			packers.RemoveWhere(comp => String.Equals(comp.PlugInPath, pluginPath, StringComparison.OrdinalIgnoreCase));
		}
	}
}
