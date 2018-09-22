using System;
using System.Collections.Generic;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal interface IEncodeMode {
		CryptProcessor EmitDecrypt(CEContext ctx);
		uint[] Encrypt(uint[] data, int offset, uint[] key);

		(PlaceholderProcessor Processor, object Data) CreateDecoder(CEContext ctx);
		uint Encode(object data, CEContext ctx, uint id);
	}
}
