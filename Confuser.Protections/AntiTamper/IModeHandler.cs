using System;
using Confuser.Core;

namespace Confuser.Protections.AntiTamper {
	internal interface IModeHandler {
		void HandleInject(AntiTamperProtection parent, IConfuserContext context, IProtectionParameters parameters);
		void HandleMD(AntiTamperProtection parent, IConfuserContext context, IProtectionParameters parameters);
	}
}
