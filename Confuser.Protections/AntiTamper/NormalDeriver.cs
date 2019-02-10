using System;
using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.AntiTamper {
	internal class NormalDeriver : IKeyDeriver {
		public void Init(IConfuserContext ctx, IRandomGenerator random) {
			//
		}

		public uint[] DeriveKey(uint[] a, uint[] b) {
			var ret = new uint[0x10];
			for (int i = 0; i < 0x10; i++) {
				switch (i % 3) {
					case 0:
						ret[i] = a[i] ^ b[i];
						break;
					case 1:
						ret[i] = a[i] * b[i];
						break;
					case 2:
						ret[i] = a[i] + b[i];
						break;
				}
			}

			return ret;
		}

		CryptProcessor IKeyDeriver.EmitDerivation(IConfuserContext ctx) => (module, method, block, key) => {
			var ret = new List<Instruction>(10 * 0x10);

			OpCode getCode(int index) {
				switch (index % 3) {
					case 0: return OpCodes.Xor;
					case 1: return OpCodes.Mul;
					case 2: return OpCodes.Add;
					default: throw new NotImplementedException();
				}
			}

			for (int i = 0; i < 0x10; i++) {
				ret.Add(Instruction.Create(OpCodes.Ldloc, block));
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				ret.Add(Instruction.Create(OpCodes.Ldloc, block));
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				ret.Add(Instruction.Create(OpCodes.Ldelem_U4));
				ret.Add(Instruction.Create(OpCodes.Ldloc, key));
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				ret.Add(Instruction.Create(OpCodes.Ldelem_U4));
				ret.Add(Instruction.Create(getCode(i)));
				ret.Add(Instruction.Create(OpCodes.Stelem_I4));
			}

			return ret;
		};
	}
}
