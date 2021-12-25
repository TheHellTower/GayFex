using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Confuser.Core.Project;
using Microsoft.Extensions.Logging;

namespace Confuser.Core {
	/// <summary>
	///     Discovers available protection plug-ins.
	/// </summary>
	public class PluginDiscovery {
		/// <summary>
		///     The default plug-in discovery service.
		/// </summary>
		internal static readonly PluginDiscovery Instance = new PluginDiscovery();

		/// <summary>
		///     Initializes a new instance of the <see cref="PluginDiscovery" /> class.
		/// </summary>
		protected PluginDiscovery() {
		}

		/// <summary>
		///     Retrieves the available protection plug-ins.
		/// </summary>
		/// <param name="project">The project supplying additional plug-in paths.</param>
		public CompositionContainer GetPlugins(ConfuserProject project, ILogger logger) {
			var catalog = new AggregateCatalog(
				DefaultPlugInDiscovery.Instance.GetDefaultPlugIns(logger),
				GetAdditionalPlugIns(project, logger));

			return new CompositionContainer(catalog);
		}

		protected virtual AggregateCatalog GetAdditionalPlugIns(ConfuserProject project, ILogger logger) {
			var result = new List<ComposablePartCatalog>();

			foreach (string pluginPath in project.PluginPaths) {
				try {
					if (File.Exists(pluginPath)) {
						result.Add(new AssemblyCatalog(Assembly.LoadFile(pluginPath)));
					}
					else if (Directory.Exists(pluginPath)) {
						result.Add(new DirectoryCatalog(pluginPath));
					}
					else {
						logger.LogWarning("Plug-in path {0} does not seem to be valid.", pluginPath);
					}
				}
				catch (Exception ex) {
					logger.LogWarning(ex, "Failed to load plug-in '{0}'.", pluginPath);
				}
			}

			return new AggregateCatalog(result);
		}

		private sealed class DefaultPlugInDiscovery {
			internal static readonly DefaultPlugInDiscovery Instance = new DefaultPlugInDiscovery();

			private AggregateCatalog DefaultPlugIns;


			/// <summary>
			///     Retrieves the available protection plugins.
			/// </summary>
			/// <param name="context">The working context.</param>
			/// <param name="protections">The working list of protections.</param>
			/// <param name="packers">The working list of packers.</param>
			/// <param name="components">The working list of components.</param>
			internal AggregateCatalog GetDefaultPlugIns(ILogger logger) {
				if (DefaultPlugIns == null) {
					DefaultPlugIns = GetDefaultPlugInsInternal(logger);
				}

				return DefaultPlugIns;
			}

			private AggregateCatalog GetDefaultPlugInsInternal(ILogger logger) {
				var result = new List<ComposablePartCatalog>();

				result.Add(new AssemblyCatalog(typeof(PluginDiscovery).Assembly));
				LoadAssemblyCatalog("Confuser.Analysis", result);
				LoadAssemblyCatalog("Confuser.Optimizations", result);
				LoadAssemblyCatalog("Confuser.Protections", result);
				LoadAssemblyCatalog("Confuser.Renamer", result);
				LoadAssemblyCatalog("Confuser.DynCipher", result);

				return new AggregateCatalog(result);
			}

			private static void LoadAssemblyCatalog(string assemblyName, IList<ComposablePartCatalog> catalogs) {
				Debug.Assert(catalogs != null, $"{nameof(catalogs)} != null");
				Debug.Assert(assemblyName != null, $"{nameof(assemblyName)} != null");

				try {
					catalogs.Add(new AssemblyCatalog(Assembly.Load(assemblyName)));
				}
				catch (Exception) {
					//logger.WarnException("Failed to load built-in protections.", ex);
				}
			}
		}
	}
}
