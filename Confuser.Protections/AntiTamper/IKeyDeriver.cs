using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.AntiTamper {
	internal interface IKeyDeriver {
		void Init(IConfuserContext ctx, IRandomGenerator random);
		uint[] DeriveKey(uint[] a, uint[] b);
		CryptProcessor EmitDerivation(IConfuserContext ctx);
	}
}
