using dnlib.DotNet;

namespace Confuser.Core.Project {
	public interface IPattern {
		bool Evaluate(IDnlibDef definition);
	}
}
