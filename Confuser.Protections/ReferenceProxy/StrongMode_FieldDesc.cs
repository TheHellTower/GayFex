using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed partial class StrongMode {
		private sealed class FieldDesc {
			public FieldDef Field { get; }
			public InitMethodDesc InitDesc { get; }
			public IMethod Method { get; }
			public Code OpCode { get; }
			public byte OpKey { get; }

			internal FieldDesc(FieldDef field, InitMethodDesc initDesc, IMethod method, Code opCode, byte opKey) {
				Field = field ?? throw new ArgumentNullException(nameof(field));
				InitDesc = initDesc ?? throw new ArgumentNullException(nameof(initDesc));
				Method = method ?? throw new ArgumentNullException(nameof(method));
				OpCode = opCode;
				OpKey = opKey;
			}
		}
	}
}
