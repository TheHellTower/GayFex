using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using NuGet.Frameworks;

namespace Confuser.Core.Services {
	partial class RuntimeService {
		private sealed class RuntimeModuleBuilder : IRuntimeModuleBuilder {
			private ImmutableDictionary<NuGetFramework, Implementation> _implementationFactories
				= ImmutableDictionary.Create<NuGetFramework, Implementation>();

			void IRuntimeModuleBuilder.AddImplementation(string targetFrameworkName, Func<Stream> assemblyStreamFactory, Func<Stream> debugSymbolStreamFactory) {
				if (targetFrameworkName == null) throw new ArgumentNullException(nameof(targetFrameworkName));
				if (assemblyStreamFactory == null) throw new ArgumentNullException(nameof(assemblyStreamFactory));
				if (string.IsNullOrWhiteSpace(targetFrameworkName)) throw new ArgumentException("Framework identifier must not be empty or white-space only.", nameof(targetFrameworkName));

				NuGetFramework framework;
				try {
					framework = NuGetFramework.Parse(targetFrameworkName);
				} catch (ArgumentException ex) {
					throw new ArgumentException("Failed to identify framework: " + targetFrameworkName, nameof(targetFrameworkName), ex);
				}

				if (!ImmutableInterlocked.TryAdd(ref _implementationFactories, framework, new Implementation(framework, assemblyStreamFactory, debugSymbolStreamFactory)))
					throw new ArgumentException("There is already a implementation for the framework: " + targetFrameworkName, nameof(targetFrameworkName));

			}

			internal (NuGetFramework ActualFramework, Func<Stream> AssemblyStreamFactory, Func<Stream> DebugSymbolStreamFactory) GetImplementationFactory(NuGetFramework requestedFramework) {
				Debug.Assert(requestedFramework != null, $"{nameof(requestedFramework)} != null");

				var nearestMatch = _implementationFactories.Values.GetNearest(requestedFramework);
				if (nearestMatch == null) throw new ArgumentException("Runtime not implemented for: " + requestedFramework, nameof(requestedFramework));
				return nearestMatch.ToValueTuple();
			}

			private sealed class Implementation : IFrameworkSpecific {
				public NuGetFramework TargetFramework { get; }
				public Func<Stream> AssemblyStreamFactory { get; }
				public Func<Stream> DebugSymbolStreamFactory { get; }
				public Implementation(NuGetFramework targetFramework, Func<Stream> assemblyStreamFactory, Func<Stream> debugSymbolStreamFactory) {
					TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
					AssemblyStreamFactory = assemblyStreamFactory ?? throw new ArgumentNullException(nameof(assemblyStreamFactory));
					DebugSymbolStreamFactory = debugSymbolStreamFactory ?? throw new ArgumentNullException(nameof(debugSymbolStreamFactory));
				}

				public (NuGetFramework, Func<Stream>, Func<Stream>) ToValueTuple()
					=> (TargetFramework, AssemblyStreamFactory, DebugSymbolStreamFactory);
			}
		}
	}
}
