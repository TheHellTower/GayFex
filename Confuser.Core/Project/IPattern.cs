using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace Confuser.Core.Project {
	public interface IPattern {
		bool Evaluate(IDnlibDef definition);
	}
}
