using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Helpers {
	/// <summary>
	/// This interface specifies extensions to the inject process that are able to modify a specific method.
	/// </summary>
	public interface IMethodInjectProcessor {
		void Process(MethodDef method);
	}
}
