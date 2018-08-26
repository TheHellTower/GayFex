using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace Confuser.Helpers {
	public interface IInjectBehavior {
		void Process(TypeDef source, TypeDefUser injected);
		void Process(MethodDef source, MethodDefUser injected);
		void Process(FieldDef source, FieldDefUser injected);
	}
}
