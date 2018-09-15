using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;

namespace Confuser.Protections.Compress {
	internal enum Mode {
		Normal,
		Dynamic
	}

	internal interface IKeyDeriver {
		void Init(IConfuserContext ctx, IRandomGenerator random);
		uint[] DeriveKey(uint[] a, uint[] b);
		CryptProcessor EmitDerivation(IConfuserContext ctx);
	}
}
