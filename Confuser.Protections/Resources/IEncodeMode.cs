using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Confuser.Helpers;

namespace Confuser.Protections.Resources {
	internal interface IEncodeMode {
		CryptProcessor EmitDecrypt(REContext ctx);
		void Encrypt(ReadOnlySpan<uint> data, ReadOnlySpan<uint> key, Span<uint> dest);
	}
}
