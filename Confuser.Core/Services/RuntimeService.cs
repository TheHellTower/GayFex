using System;
using System.IO;
using System.Reflection;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	internal class RuntimeService : IRuntimeService {
		ModuleDef rtModule;

		/// <inheritdoc />
		public TypeDef GetRuntimeType(string fullName) {
			if (rtModule == null) {
				LoadConfuserRuntimeModule();
			}
			return rtModule.Find(fullName, true);
		}

		private void LoadConfuserRuntimeModule() {
			const string runtimeDllName = "Confuser.Runtime.dll";

			var module = typeof(RuntimeService).Assembly.ManifestModule;
			string rtPath = runtimeDllName;
			var creationOptions = new ModuleCreationOptions() { TryToLoadPdbFromDisk = true };
			if (module.FullyQualifiedName[0] != '<') {
				rtPath = Path.Combine(Path.GetDirectoryName(module.FullyQualifiedName), rtPath);
				if (File.Exists(rtPath)) {
					try {
						rtModule = ModuleDefMD.Load(rtPath, creationOptions);
					}
					catch (IOException) { }
				}
				if (rtModule == null) {
					rtPath = runtimeDllName;
				}
			}
			if (rtModule == null) {
				rtModule = ModuleDefMD.Load(rtPath, creationOptions);
			}
			rtModule.EnableTypeDefFindCache = true;
		}
	}
}
