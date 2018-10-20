using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class TypeRefFinder {
		private readonly ModuleDef _module;

		internal TypeRefFinder(ModuleDef module) => 
			_module = module ?? throw new ArgumentNullException(nameof(module));

		internal ITypeDefOrRef FindType(string fullName) {
			var processedModules = new HashSet<ModuleDef>() { _module };
			var modulesToScan = new Queue<ModuleDef>();
			modulesToScan.Enqueue(_module);

			while (modulesToScan.Count > 0) {
				var currentModule = modulesToScan.Dequeue();
				var definedInModule = currentModule.FindNormal(fullName);
				if (definedInModule != null)
					return definedInModule;

				foreach (var typeRef in currentModule.GetTypeRefs()) {
					if (typeRef.FullName.Equals(fullName, StringComparison.Ordinal))
						return typeRef;

					var resolvedType = typeRef.Resolve();
					if (resolvedType != null && processedModules.Add(resolvedType.Module)) {
						modulesToScan.Enqueue(resolvedType.Module);
					}
				}
			}

			throw new InvalidOperationException($"Could not find the type {fullName}.");
		}
	}
}
