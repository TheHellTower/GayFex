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
			foreach (var prot in container.GetExports<IProtection>()) {
				ctx.AddProtection(Info.FromComponent(prot.Value, ctx.PluginPath));
			}
			foreach (var packer in container.GetExports<IPacker>()) {
				ctx.AddPacker(Info.FromComponent(packer.Value, ctx.PluginPath));
			}
		}

		public static void LoadComponents(IList<IConfuserComponent> protections, IList<IConfuserComponent> packers, string pluginPath) {
			var ctx = new CrossDomainContext(protections, packers, pluginPath);
			AppDomain appDomain = AppDomain.CreateDomain("");
			appDomain.SetData("ctx", ctx);
			appDomain.DoCallBack(CrossDomainLoadComponents);
			AppDomain.Unload(appDomain);
		}

		public static void RemoveComponents(IList<IConfuserComponent> protections, IList<IConfuserComponent> packers, string pluginPath) {
			protections.RemoveWhere(comp => comp is InfoComponent && ((InfoComponent)comp).info.path == pluginPath);
			packers.RemoveWhere(comp => comp is InfoComponent && ((InfoComponent)comp).info.path == pluginPath);
		}

		class CrossDomainContext : MarshalByRefObject {
			readonly IList<IConfuserComponent> packers;
			readonly string pluginPath;
			readonly IList<IConfuserComponent> protections;

			public CrossDomainContext(IList<IConfuserComponent> protections, IList<IConfuserComponent> packers, string pluginPath) {
				this.protections = protections;
				this.packers = packers;
				this.pluginPath = pluginPath;
			}

			public string PluginPath {
				get { return pluginPath; }
			}

			public void AddProtection(Info info) {
				foreach (var comp in protections) {
					if (comp.Id == info.id)
						return;
				}
				protections.Add(new InfoComponent(info));
			}

			public void AddPacker(Info info) {
				foreach (var comp in packers) {
					if (comp.Id == info.id)
						return;
				}
				packers.Add(new InfoComponent(info));
			}
		}

		[Serializable]
		class Info {
			public string desc;
			public string fullId;
			public string id;
			public string name;
			public string path;

			public static Info FromComponent(IConfuserComponent component, string pluginPath) {
				var ret = new Info();
				ret.name = component.Name;
				ret.desc = component.Description;
				ret.id = component.Id;
				ret.fullId = component.FullId;
				ret.path = pluginPath;
				return ret;
			}
		}

		class InfoComponent : IConfuserComponent {
			public readonly Info info;

			public InfoComponent(Info info) {
				this.info = info;
			}

			public string Name => info.name;

			public string Description => info.desc;

			public string Id => info.id;

			public string FullId => info.fullId;

			void IConfuserComponent.Initialize(IServiceCollection context) => throw new NotSupportedException();

			void IConfuserComponent.PopulatePipeline(IProtectionPipeline pipeline) => throw new NotSupportedException();
		}
	}
}
