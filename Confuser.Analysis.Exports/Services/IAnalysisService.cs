using System;
using dnlib.DotNet;

namespace Confuser.Analysis.Services {
	public interface IAnalysisService
	{
		IVTable GetVTable(ITypeDefOrRef typeDefOrRef);

		(ModuleFramework, Version?) IdentifyModuleFramework(ModuleDef moduleDef);
	}
}
