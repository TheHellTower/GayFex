using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;

namespace Confuser.Helpers {
	public interface IInjectBehavior {
		void Process(TypeDef source, TypeDefUser injected, Importer importer);
		void Process(MethodDef source, MethodDefUser injected, Importer importer);
		void Process(FieldDef source, FieldDefUser injected, Importer importer);
		void Process(EventDef source, EventDefUser injected, Importer importer);
		void Process(PropertyDef source, PropertyDefUser injected, Importer importer);
	}
}
