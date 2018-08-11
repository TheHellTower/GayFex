using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confuser.Core;
using Confuser.Protections.Services;
using dnlib.DotNet;

namespace Confuser.Protections.AntiTamper {
	internal sealed class AntiTamperService : IAntiTamperService {
		private readonly AntiTamperProtection protection;

		internal AntiTamperService(AntiTamperProtection protection) =>
			this.protection = protection ?? throw new ArgumentNullException(nameof(protection));

		public void ExcludeMethod(IConfuserContext context, MethodDef method) {
			context.GetParameters(method).RemoveParameters(protection);
		}
	}
}
