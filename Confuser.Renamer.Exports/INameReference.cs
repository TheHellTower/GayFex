using Confuser.Core;
using Confuser.Renamer.Services;

namespace Confuser.Renamer {
	public interface INameReference {
		bool UpdateNameReference(IConfuserContext context, INameService service);

		bool ShouldCancelRename();
	}

	public interface INameReference<out T> : INameReference { }
}
