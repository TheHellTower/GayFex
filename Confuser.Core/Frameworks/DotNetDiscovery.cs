using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Core.Frameworks {
	/// <summary>
	/// Discovers .NET and .NET Core runtimes on the local system.
	/// </summary>
	[Export(typeof(IFrameworkDiscovery))]
	internal sealed class DotNetDiscovery : IFrameworkDiscovery {
		private List<IInstalledFramework> InstalledFrameworks { get; set; }

		public IEnumerable<IInstalledFramework> GetInstalledFrameworks(IServiceProvider services)
			=> InstalledFrameworks ??= DiscoverFrameworks(services ?? throw new ArgumentNullException(nameof(services))).Distinct().ToList();

		private static IEnumerable<IInstalledFramework> DiscoverFrameworks(IServiceProvider services) {
			var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("framework.discovery");

			var processStartInfo = new ProcessStartInfo {
				FileName = "dotnet",
				Arguments = "--list-runtimes",
				RedirectStandardOutput = true
			};

			var runtimeLines = new List<string>();
			try {
				using var process = Process.Start(processStartInfo);

				var stdOut = process.StandardOutput;
				string line;
				while ((line = stdOut.ReadLine()) is not null) {
					runtimeLines.Add(line);
				}
			} catch (FileNotFoundException ex) {
				logger.LogWarning(ex, "Failed to invoke dotnet.");
			}

			return runtimeLines.Select(line => {
				var match = Regex.Match(line, @"^([\w\.]+)\s+([\d\.]+)\s+\[([^\]]+)\]$", RegexOptions.CultureInvariant);
				if (match.Success && Version.TryParse(match.Groups[2].Value, out var version)) {
					return (
						FrameworkType: match.Groups[1].Value, 
						FrameworkVersion: version, 
						RootPath: new DirectoryInfo(Path.Combine(match.Groups[3].Value, match.Groups[2].Value))
					);
				}
				return (null, null, null);
			})
				.Where(def => !string.IsNullOrWhiteSpace(def.FrameworkType) && def.RootPath.Exists)
				.GroupBy(def => def.FrameworkVersion, def => (def.FrameworkType, def.RootPath))
			    .Select(r => {
					try {
						return new InstalledDotNet(r);
					} catch (ConfuserException ex) {
						logger.LogWarning(ex, "Unexpected state of .NET Core/.NET runtime.");
					}
					return null;
				})
				.Where(i => i is not null);
		}

		private sealed class InstalledDotNet : IInstalledFramework, IEquatable<InstalledDotNet> {
			public ModuleFramework ModuleFramework => ModuleFramework.DotNet;

			public Version Version { get; }

			private DirectoryInfo MainDirectory { get; }
			private IReadOnlyList<DirectoryInfo> ExtensionDirectories { get; }


			internal InstalledDotNet(IGrouping<Version, (string FrameworkType, DirectoryInfo RootPath)> runtimes) {
				Version = runtimes.Key;

				DirectoryInfo mainDirectory = null;
				List<DirectoryInfo> extensionDirectories = new();
				
				foreach (var runtime in runtimes) {
					if (!runtime.RootPath.Exists) continue;
					if (runtime.FrameworkType.Equals("Microsoft.NETCore.App", StringComparison.Ordinal)) {
						if (mainDirectory is not null)
							throw new ConfuserException($"Got the main part of .NET Core/.NET twice for version {Version}");
						mainDirectory = runtime.RootPath;
					} else {
						extensionDirectories.Add(runtime.RootPath);
					}
				}

				if (mainDirectory is null)
					throw new ConfuserException($"Failed to resolve the main component of .NET Core/.NET for version {Version}");

				MainDirectory = mainDirectory;
				ExtensionDirectories = extensionDirectories;
			}

			public IAssemblyResolver CreateAssemblyResolver() {
				var resolver = new AssemblyResolver() { UseGAC = false };
				resolver.PostSearchPaths.Add(MainDirectory.FullName);
				foreach (var extensionPath in ExtensionDirectories) {
					resolver.PostSearchPaths.Add(extensionPath.FullName);
				}
				return resolver;
			}

			public bool Equals(IInstalledFramework other) => Equals(other as InstalledDotNet);
			public bool Equals(InstalledDotNet other) => other is not null && Version.Equals(other.Version);
			public override bool Equals(object obj) => Equals(obj as InstalledDotNet);
			public override int GetHashCode() => Version.GetHashCode();
			public override string ToString() {
				if (Version.Major < 5) {
					return $".NET Core {Version}";
				} else {
					return $".NET {Version}";
				}
			}
		}
	}
}
