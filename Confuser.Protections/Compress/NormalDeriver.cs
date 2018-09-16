using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			Debug.Assert(ctx != null, $"{nameof(ctx)} != null");
			Debug.Assert(random != null, $"{nameof(random)} != null");

			k1 = random.NextUInt32() | 1;
			k2 = random.NextUInt32() | 1;
			k3 = random.NextUInt32() | 1;
			seed = random.NextUInt32();
		}

		void IKeyDeriver.DeriveKey(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> key) {
			Debug.Assert(a.Length == 0x10, $"{nameof(a)}.Length == 0x10");
			Debug.Assert(b.Length == 0x10, $"{nameof(b)}.Length == 0x10");
			Debug.Assert(key.Length == 0x10, $"{nameof(key)}.Length == 0x10");
			
			var state = seed;
			for (int i = 0; i < 0x10; i++) {
				switch (state % 3) {
					case 0:
						key[i] = a[i] ^ b[i];
						break;
					case 1:
						key[i] = a[i] * b[i];
						break;
					case 2:
						key[i] = a[i] + b[i];
						break;
				}
				state = (state * state) % 0x2E082D35;
				switch (state % 3) {
					case 0:
						key[i] += k1;
						break;
					case 1:
						key[i] ^= k2;
						break;
					case 2:
						key[i] *= k3;
						break;
				}
				state = (state * state) % 0x2E082D35;
			}
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
