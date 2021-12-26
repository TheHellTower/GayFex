using System;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Analysis {
	/// <summary>
	/// This class contains everything currently implemented to identify the framework version of an specific module.
	/// </summary>
	internal static class ModuleFrameworkAnalyzer {
		private const string _tfmFramework = ".NETFramework";
		private const string _tfmUwp = ".NETCore";
		private const string _tfmCore = ".NETCoreApp";
		private const string _tfmStandard = ".NETStandard";

		internal static (ModuleFramework Framework, Version? Version) IdenitfyFramework(ModuleDef moduleDef) {
			if (moduleDef is null) throw new ArgumentNullException(nameof(moduleDef));

			var framework = TryIdentifyByAttribute(moduleDef, out var version);
			if (framework != ModuleFramework.Unknown) {
				return (framework, version);
			}
			return (ModuleFramework.Unknown, null);
		}

		private static ModuleFramework TryIdentifyByAttribute(ModuleDef moduleDef, out Version? version) {
			var asmDef = moduleDef.Assembly;
			if (asmDef is null || !asmDef.TryGetOriginalTargetFrameworkAttribute(out string frameworkMoniker, out version, out _)) {
				version = null;
				return ModuleFramework.Unknown;
			}

			return frameworkMoniker switch {
				_tfmFramework => ModuleFramework.DotNetFramework,
				_tfmCore => ModuleFramework.DotNet,
				_tfmStandard => ModuleFramework.DotNetStandard,
				_tfmUwp => ModuleFramework.Uwp,
				_ => ModuleFramework.Unknown,
			};
		}
	}
}
