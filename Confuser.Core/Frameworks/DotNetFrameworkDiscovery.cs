using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Confuser.Core.Frameworks {
	[Export(typeof(IFrameworkDiscovery))]
	internal sealed class DotNetFrameworkDiscovery : IFrameworkDiscovery {
		private List<IInstalledFramework> InstalledFrameworks { get; set; }

		public IEnumerable<IInstalledFramework> GetInstalledFrameworks(IServiceProvider services)
			=> InstalledFrameworks ??= DiscoverFrameworks(services ?? throw new ArgumentNullException(nameof(services))).Distinct().ToList();

		private static IEnumerable<IInstalledFramework> DiscoverFrameworks(IServiceProvider services) {
			var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("framework.discovery");

			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				logger.LogTrace("Skipping .NET Framework discovery, due to non-windows platform.");
				yield break;
			}

			// http://msdn.microsoft.com/en-us/library/hh925568.aspx
			using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\")) {
				foreach (string versionKeyName in ndpKey.GetSubKeyNames()) {
					if (!versionKeyName.StartsWith("v")) continue;
					if (versionKeyName.Equals("v4", StringComparison.Ordinal)) continue;

					using RegistryKey versionKey = ndpKey.OpenSubKey(versionKeyName);
					var versionNumber = versionKey.GetValue("Version", "").ToString();
					bool install = versionKey.GetValue("Install", "").ToString() == "1";
					if (install && Version.TryParse(versionNumber, out var version)) {
						yield return new InstalledDotNetFramework(version);
					}

					if (!string.IsNullOrWhiteSpace(versionNumber)) continue;

					foreach (string subKeyName in versionKey.GetSubKeyNames()) {
						using RegistryKey subKey = versionKey.OpenSubKey(subKeyName);
						versionNumber = (string)subKey.GetValue("Version", "");
						install = subKey.GetValue("Install", "").ToString() == "1";

						if (install && Version.TryParse(versionNumber, out version)) {
							yield return new InstalledDotNetFramework(version);
						}
					}
				}
			}

			using (RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")) {
				var versionNumber = ndpKey.GetValue("Version", "").ToString();
				bool install = ndpKey.GetValue("Install", "").ToString() == "1";
				if (install && Version.TryParse(versionNumber, out var version)) {
					yield return new InstalledDotNetFramework(version);
				}
			}
		}

		private sealed class InstalledDotNetFramework : IInstalledFramework, IEquatable<InstalledDotNetFramework> {
			public ModuleFramework ModuleFramework => ModuleFramework.DotNetFramework;

			public Version Version { get; }

			internal InstalledDotNetFramework(Version version) {
				Version = version;
			}

			public IAssemblyResolver CreateAssemblyResolver() =>
				Version.Major < 4 ? new RedirectingAssemblyResolverV2() : new RedirectingAssemblyResolverV4();

			public bool Equals(IInstalledFramework other) => Equals(other as InstalledDotNetFramework);
			public bool Equals(InstalledDotNetFramework other) => other is not null && Version.Equals(other.Version);

			public override bool Equals(object obj) => Equals(obj as InstalledDotNetFramework);
			public override int GetHashCode() => Version.GetHashCode();
			public override string ToString() => $".NET Framework v{Version}";
		}

		private abstract class RedirectingAssemblyResolver : AssemblyResolver {			
			protected RedirectingAssemblyResolver() {
				EnableFrameworkRedirect = false;
				UseGAC = true;
			}

			protected override IEnumerable<string> FindAssemblies(IAssembly assembly, ModuleDef sourceModule, bool matchExactly) {
				ApplyFrameworkRedirect(ref assembly);
				return base.FindAssemblies(assembly, sourceModule, matchExactly);
			}

			protected abstract void ApplyFrameworkRedirect(ref IAssembly assembly);
		}

		private sealed class RedirectingAssemblyResolverV2 : RedirectingAssemblyResolver {
			protected override void ApplyFrameworkRedirect(ref IAssembly assembly) => FrameworkRedirect.ApplyFrameworkRedirectV2(ref assembly);
		}

		private sealed class RedirectingAssemblyResolverV4 : RedirectingAssemblyResolver {
			protected override void ApplyFrameworkRedirect(ref IAssembly assembly) => FrameworkRedirect.ApplyFrameworkRedirectV4(ref assembly);
		}
	}
}
