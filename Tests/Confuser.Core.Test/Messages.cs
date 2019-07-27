using System.Runtime.CompilerServices;

namespace Confuser.Core {
	internal static class Messages {
		internal static string UnexpectedCallMsgFormat([CallerMemberName] string memberName = null) =>
			string.Format(Properties.Resources.Culture, Properties.Resources.UnexpectedCallMsgFormat, memberName);
	}
}