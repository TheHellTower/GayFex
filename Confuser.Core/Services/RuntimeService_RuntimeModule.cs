using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using NuGet.Frameworks;

namespace Confuser.Core.Services {
	internal partial class RuntimeService {
		private sealed class RuntimeModule : IRuntimeModule {
			private RuntimeModuleBuilder Builder { get; }

			private ImmutableDictionary<NuGetFramework, ModuleDef> _runtimeTypes;

			internal RuntimeModule(RuntimeModuleBuilder builder) {
				Debug.Assert(builder != null, $"{nameof(builder)} != null");

				Builder = builder;
				_runtimeTypes = ImmutableDictionary.Create<NuGetFramework, ModuleDef>();
			}

			TypeDef IRuntimeModule.GetRuntimeType(string fullName, ModuleDef targetModule) {
				if (fullName == null) throw new ArgumentNullException(nameof(fullName));
				if (targetModule == null) throw new ArgumentNullException(nameof(targetModule));

				var runtimeModule = GetRuntimeModule(targetModule);
				return runtimeModule.Find(fullName, false);
			}

			private ModuleDef GetRuntimeModule(ModuleDef targetModule) {
				Debug.Assert(targetModule != null, $"{nameof(targetModule)} != null");

				var moduleFramework = IdentifyFramework(targetModule);
				if (_runtimeTypes.TryGetValue(moduleFramework, out var existingRequestedRuntimeModule))
					return existingRequestedRuntimeModule;

				var factoryData = Builder.GetImplementationFactory(moduleFramework);
				if (_runtimeTypes.TryGetValue(factoryData.ActualFramework, out var existingActualRuntimeModule)) {
					ImmutableInterlocked.TryAdd(ref _runtimeTypes, moduleFramework, existingActualRuntimeModule);
					return existingActualRuntimeModule;
				}

				var creationOptions = new ModuleCreationOptions() {
					PdbFileOrData = factoryData.DebugSymbolStreamFactory?.Invoke()
				};
				var runtimeModule = ModuleDefMD.Load(factoryData.AssemblyStreamFactory.Invoke(), creationOptions);
				runtimeModule.EnableTypeDefFindCache = true;
				ImmutableInterlocked.TryAdd(ref _runtimeTypes, moduleFramework, runtimeModule);
				ImmutableInterlocked.TryAdd(ref _runtimeTypes, factoryData.ActualFramework, runtimeModule);

				return runtimeModule;
			}

			private static NuGetFramework IdentifyFramework(ModuleDef targetModule) {
				Debug.Assert(targetModule != null, $"{nameof(targetModule)} != null");

				if (targetModule.IsClr10) {
					// Behold! The ancient .NET Framework 1.0.
					return new NuGetFramework(".NETFramework", new Version(1, 0, 0, 0));
				}
				else if (targetModule.IsClr11) {
					return FrameworkConstants.CommonFrameworks.Net11;
				}
				else if (targetModule.IsClr20) {
					// This may be .NET Framework 2.0 or 3.5.
					// Lets see if there are any indicates that this is actually the .NET Framework 3.5
					if (targetModule.GetModuleRefs().Where(IndicatesNetFramework35).Any())
						return FrameworkConstants.CommonFrameworks.Net35;

					return FrameworkConstants.CommonFrameworks.Net2;
				}
				else if (targetModule.IsClr40 || targetModule.RuntimeVersion == null) {
					// CLR 4 Assemblies got the TargetFrameworkAttribute
					// We also assume a CLR 4 assembly in case the runtime version is not set. May point to a module
					// that was newly created and was not yet written to the hard drive. In case this is wrong, it
					// can be easily fixed by just setting the runtime version properly.

					if (targetModule.Assembly.HasCustomAttributes) {
						var customAttributes = targetModule.Assembly.CustomAttributes;
						var targetFrameworkAttribute = customAttributes
							.Where(ca => ca.TypeFullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
							.FirstOrDefault();

						if (targetFrameworkAttribute != null) {
							var frameworkName = targetFrameworkAttribute.ConstructorArguments[0].Value?.ToString();
							if (!string.IsNullOrWhiteSpace(frameworkName))
								return NuGetFramework.Parse(frameworkName);
						}
					}

					// For some reason the attribute is missing. We default to the oldest compatible version.
					return FrameworkConstants.CommonFrameworks.Net4;
				}

				throw new NotImplementedException(
					"Unknown common language runtime. Assembly is not compatible with ConfuserEx runtime system.");
			}

			private static bool IndicatesNetFramework35(ModuleRef module) {
				Debug.Assert(module != null, $"{nameof(module)} != null");

				switch (module.FullName) {
					case "System.Net, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a":
					case "System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089":
					case
						"System.Data.DataSetExtensions, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
						:
					case "System.AddIn, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089":
					case "System.Xml.Linq, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089":
					case "System.ServiceModel.Web, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35":
					case
						"System.Windows.Presentation, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
						:
					case
						"System.Data.Services.Client, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
						:
						return true;
					default:
						return false;
				}
			}
		}
	}
}
