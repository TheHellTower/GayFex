using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Reflection;
using Confuser.Core.Project;

namespace Confuser.Core {
	/// <summary>
	///     Discovers available protection plugins.
	/// </summary>
	public class PluginDiscovery {
		/// <summary>
		///     The default plugin discovery service.
		/// </summary>
		internal static readonly PluginDiscovery Instance = new PluginDiscovery();

		/// <summary>
		///     Initializes a new instance of the <see cref="PluginDiscovery" /> class.
		/// </summary>
		protected PluginDiscovery() { }

		/// <summary>
		///     Retrieves the available protection plugins.
		/// </summary>
		/// <param name="project">The project supplying additional plugin paths.</param>
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
						logger.Warn($"Plugin path {pluginPath} does not seem to be valid.");
					}
				}
				catch (Exception ex) {
					logger.WarnException("Failed to load plugin '" + pluginPath + "'.", ex);
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
				try {
					result.Add(new AssemblyCatalog(Assembly.Load("Confuser.Protections")));
				}
				catch (Exception) {
					//logger.WarnException("Failed to load built-in protections.", ex);
				}

				try {
					result.Add(new AssemblyCatalog(Assembly.Load("Confuser.Renamer")));
				}
				catch (Exception) {
					//logger.WarnException("Failed to load renamer.", ex);
				}

				try {
					result.Add(new AssemblyCatalog(Assembly.Load("Confuser.DynCipher")));
				}
				catch (Exception) {
					//logger.WarnException("Failed to load dynamic cipher library.", ex);
				}

				return new AggregateCatalog(result);
			}
		}
	}
}
