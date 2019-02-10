using Confuser.Core;
using Confuser.Renamer.Services;
using dnlib.DotNet;

namespace Confuser.Renamer {
	public interface IRenamer {
		void Analyze(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def);
		void PreRename(IConfuserContext context, INameService service, IProtectionParameters parameters, IDnlibDef def);

		void PostRename(IConfuserContext context, INameService service, IProtectionParameters parameters,
			IDnlibDef def);
	}
}
