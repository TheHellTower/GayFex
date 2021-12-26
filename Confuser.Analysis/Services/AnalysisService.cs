using System;
using Confuser.Core;
using dnlib.DotNet;

namespace Confuser.Analysis.Services {
	internal sealed class AnalysisService : IAnalysisService {
		private VTableStorage VTableStorage { get; }

		public AnalysisService(IServiceProvider serviceProvider) {
			VTableStorage = new VTableStorage(serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)));
		}

		public VTable GetVTable(ITypeDefOrRef typeDefOrRef) => VTableStorage.GetVTable(typeDefOrRef);

		IVTable IAnalysisService.GetVTable(ITypeDefOrRef typeDefOrRef) => GetVTable(typeDefOrRef);
		public (ModuleFramework, Version?) IdentifyModuleFramework(ModuleDef moduleDef) =>
			ModuleFrameworkAnalyzer.IdenitfyFramework(moduleDef ?? throw new ArgumentNullException(nameof(moduleDef)));
	}
}
