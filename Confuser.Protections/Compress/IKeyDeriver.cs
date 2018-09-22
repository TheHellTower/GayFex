using System;
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
		void DeriveKey(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> key);
		CryptProcessor EmitDerivation(IConfuserContext ctx);
	}
}
