using System.Collections.Generic;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Helpers;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Compress {
	internal sealed class NormalDeriver : IKeyDeriver {
		private uint k1;
		private uint k2;
		private uint k3;
		private uint seed;

		void IKeyDeriver.Init(IConfuserContext ctx, IRandomGenerator random) {
			k1 = random.NextUInt32() | 1;
			k2 = random.NextUInt32() | 1;
			k3 = random.NextUInt32() | 1;
			seed = random.NextUInt32();
		}

		uint[] IKeyDeriver.DeriveKey(uint[] a, uint[] b) {
			var ret = new uint[0x10];
			var state = seed;
			for (int i = 0; i < 0x10; i++) {
				switch (state % 3) {
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
				state = (state * state) % 0x2E082D35;
				switch (state % 3) {
					case 0:
						ret[i] += k1;
						break;
					case 1:
						ret[i] ^= k2;
						break;
					case 2:
						ret[i] *= k3;
						break;
				}
				state = (state * state) % 0x2E082D35;
			}
			return ret;
		}

		CryptProcessor IKeyDeriver.EmitDerivation(IConfuserContext ctx) => (method, block, key) => {
			var state = seed;
			var result = new List<Instruction>(12 * 0x10);
			for (int i = 0; i < 0x10; i++) {
				result.Add(Instruction.Create(OpCodes.Ldloc, block));
				result.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				result.Add(Instruction.Create(OpCodes.Ldloc, block));
				result.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				result.Add(Instruction.Create(OpCodes.Ldelem_U4));
				result.Add(Instruction.Create(OpCodes.Ldloc, key));
				result.Add(Instruction.Create(OpCodes.Ldc_I4, i));
				result.Add(Instruction.Create(OpCodes.Ldelem_U4));
				switch (state % 3) {
					case 0:
						result.Add(Instruction.Create(OpCodes.Xor));
						break;
					case 1:
						result.Add(Instruction.Create(OpCodes.Mul));
						break;
					case 2:
						result.Add(Instruction.Create(OpCodes.Add));
						break;
				}
				state = (state * state) % 0x2E082D35;
				switch (state % 3) {
					case 0:
						result.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k1));
						result.Add(Instruction.Create(OpCodes.Add));
						break;
					case 1:
						result.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k2));
						result.Add(Instruction.Create(OpCodes.Xor));
						break;
					case 2:
						result.Add(Instruction.Create(OpCodes.Ldc_I4, (int)k3));
						result.Add(Instruction.Create(OpCodes.Mul));
						break;
				}
				state = (state * state) % 0x2E082D35;
				result.Add(Instruction.Create(OpCodes.Stelem_I4));
			}
			return result;
		};
	}
}
