using System.Collections.Generic;
using System.Linq;
using Confuser.Core.Services;
using Confuser.DynCipher;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ReferenceProxy {
	internal sealed class NormalEncoding : IRPEncoding {
		private readonly Dictionary<MethodDef, (int Key, int InvKey)> _keys
			= new Dictionary<MethodDef, (int, int)>();

		Helpers.PlaceholderProcessor IRPEncoding.EmitDecode(RPContext ctx) => (module, method, args) => {
			var (key, _) = GetKey(ctx.Random, method);
			var ret = new List<Instruction>(args.Count + 2);

			if (ctx.Random.NextBoolean()) {
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, key));
				ret.AddRange(args);
			}
			else {
				ret.AddRange(args);
				ret.Add(Instruction.Create(OpCodes.Ldc_I4, key));
			}

			ret.Add(Instruction.Create(OpCodes.Mul));
			return ret;
		};

		int IRPEncoding.Encode(MethodDef init, RPContext ctx, int value) {
			// Encode the value with the key of the specified method.
			var (_, invKey) = GetKey(ctx.Random, init);
			return value * invKey;
		}

		private (int Key, int InvKey) GetKey(IRandomGenerator random, MethodDef init) {
			if (_keys.TryGetValue(init, out var ret)) return ret;

			// The key for the initialization method is not generated yet. Generate the int32 key now.
			int key = random.NextInt32() | 1;
			_keys[init] = ret = (key, unchecked((int)MathsUtils.ModInv((uint)key)));

			return ret;
		}
	}
}
