using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using dnlib.DotNet;
using Microsoft.Win32;

namespace Confuser.Core.Frameworks {
	[Export(typeof(IFrameworkDiscovery))]
	internal sealed class DotNetFrameworkDiscovery : IFrameworkDiscovery {
		private List<IInstalledFramework> InstalledFrameworks { get; set; }

		public IEnumerable<IInstalledFramework> GetInstalledFrameworks()
			=> InstalledFrameworks ??= DiscoverFrameworks().Distinct().ToList();

		private static IEnumerable<IInstalledFramework> DiscoverFrameworks() {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				yield break;

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

			public AssemblyResolver CreateAssemblyResolver() => throw new NotImplementedException();

			public bool Equals(IInstalledFramework other) => Equals(other as InstalledDotNetFramework);
			public bool Equals(InstalledDotNetFramework other) => other is not null && Version.Equals(other.Version);

			public override bool Equals(object obj) => Equals(obj as InstalledDotNetFramework);
			public override int GetHashCode() => Version.GetHashCode();
			public override string ToString() => $".NET Framework v{Version}";
		}
	}
}
