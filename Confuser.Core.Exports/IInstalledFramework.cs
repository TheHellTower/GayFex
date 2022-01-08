using System;
using System.Collections.Generic;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core {
	public interface IInstalledFramework : IEquatable<IInstalledFramework> {
		ModuleFramework ModuleFramework { get; }
		Version Version { get; }

		IAssemblyResolver CreateAssemblyResolver();
	}
}
