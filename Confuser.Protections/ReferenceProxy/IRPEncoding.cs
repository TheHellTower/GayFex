using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal interface IRPEncoding {
		Helpers.PlaceholderProcessor EmitDecode(RPContext ctx);
		int Encode(MethodDef init, RPContext ctx, int value);
	}
}
