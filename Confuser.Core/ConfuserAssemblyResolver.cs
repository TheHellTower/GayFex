using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Core {
	internal sealed class ConfuserAssemblyResolver : IAssemblyResolver {
		AssemblyResolver InternalResolver { get; }

		public bool EnableTypeDefCache {
			get => InternalResolver.EnableTypeDefCache; 
			set => InternalResolver.EnableTypeDefCache = value;
		}

		public ModuleContext DefaultModuleContext {
			get => InternalResolver.DefaultModuleContext; 
			set => InternalResolver.DefaultModuleContext = value;
		}

		public IList<string> PostSearchPaths => InternalResolver.PostSearchPaths;

		internal ConfuserAssemblyResolver() => 
			InternalResolver = new AssemblyResolver();

		/// <inheritdoc />
		public AssemblyDef Resolve(IAssembly assembly, ModuleDef sourceModule) {
			if (assembly is AssemblyDef assemblyDef)
				return assemblyDef;

			var cachedAssemblyDef = InternalResolver
				.GetCachedAssemblies()
				.FirstOrDefault(a => AssemblyNameComparer.NameAndPublicKeyTokenOnly.Equals(a, assembly));
			if (!(cachedAssemblyDef is null))
				return cachedAssemblyDef;

			AssemblyDef resolvedAssemblyDef = null;
			try {
				InternalResolver.FindExactMatch = true;
				resolvedAssemblyDef = InternalResolver.Resolve(assembly, sourceModule);
			}
			finally {
				InternalResolver.FindExactMatch = false;
			}

			return resolvedAssemblyDef ?? InternalResolver.Resolve(assembly, sourceModule);
		}

		public void Clear() => InternalResolver.Clear();

		public IEnumerable<AssemblyDef> GetCachedAssemblies() => InternalResolver.GetCachedAssemblies();

		public void AddToCache(ModuleDefMD modDef) => InternalResolver.AddToCache(modDef);
	}
}
