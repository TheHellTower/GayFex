using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using Confuser.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ConfuserEx {
	internal class ComponentDiscovery {
		static void CrossDomainLoadComponents() {
			var ctx = (CrossDomainContext)AppDomain.CurrentDomain.GetData("ctx");
			// Initialize the version resolver callback
			ConfuserEngine.Version.ToString();

			Assembly assembly = Assembly.LoadFile(ctx.PluginPath);
			var catalog = new AssemblyCatalog(assembly);
			var container = new CompositionContainer(catalog);
			foreach (var prot in container.GetExports<IProtection, IProtectionMetadata>()) {
				ctx.AddProtection(new ConfuserUiComponent(prot, ctx.PluginPath));
			}

			foreach (var packer in container.GetExports<IPacker, IPackerMetadata>()) {
				ctx.AddPacker(new ConfuserUiComponent(packer, ctx.PluginPath));
			}
		}

		public static void LoadComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers,
			string pluginPath) {
			var ctx = new CrossDomainContext(protections, packers, pluginPath);
			AppDomain appDomain = AppDomain.CreateDomain("");
			appDomain.SetData("ctx", ctx);
			appDomain.DoCallBack(CrossDomainLoadComponents);
			AppDomain.Unload(appDomain);
		}

		public static void RemoveComponents(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers,
			string pluginPath) {
			protections.RemoveWhere(comp =>
				String.Equals(comp.PlugInPath, pluginPath, StringComparison.OrdinalIgnoreCase));
			packers.RemoveWhere(comp => String.Equals(comp.PlugInPath, pluginPath, StringComparison.OrdinalIgnoreCase));
		}

		class CrossDomainContext : MarshalByRefObject {
			readonly IList<ConfuserUiComponent> packers;
			readonly string pluginPath;
			readonly IList<ConfuserUiComponent> protections;

			public CrossDomainContext(IList<ConfuserUiComponent> protections, IList<ConfuserUiComponent> packers,
				string pluginPath) {
				this.protections = protections;
				this.packers = packers;
				this.pluginPath = pluginPath;
			}

			public string PluginPath {
				get { return pluginPath; }
			}

			public void AddProtection(ConfuserUiComponent uiComponent) {
				if (protections.Contains(uiComponent)) return;

				protections.Add(uiComponent);
			}

			public void AddPacker(ConfuserUiComponent uiComponent) {
				if (packers.Contains(uiComponent)) return;

				packers.Add(uiComponent);
			}
		}
	}
}
