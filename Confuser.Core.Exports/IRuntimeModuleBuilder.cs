using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Confuser.Core {
	public interface IRuntimeModuleBuilder {
		void AddImplementation(string targetFrameworkName, Func<Stream> assemblyStreamFactory, Func<Stream> debugSymbolStreamFactory);
	}
}
