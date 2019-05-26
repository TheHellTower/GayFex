using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class NumOp : CryptoElement {
		public NumOp()
			: base(1) {
		}

		public uint Key { get; private set; }
		public uint InverseKey { get; private set; }
		public CryptoNumOps Operation { get; private set; }

		public override void Initialize(IRandomGenerator random) {
			Operation = (CryptoNumOps)(random.NextInt32(4));
			switch (Operation) {
				case CryptoNumOps.Add:
				case CryptoNumOps.Xor:
					Key = InverseKey = random.NextUInt32();
					break;
				case CryptoNumOps.Mul:
					Key = random.NextUInt32() | 1;
					InverseKey = MathsUtils.ModInv(Key);
					break;
				case CryptoNumOps.Xnor:
					Key = random.NextUInt32();
					InverseKey = ~Key;
					break;
			}
		}

		public override void Emit(CipherGenContext context) {
			var val = context.GetDataExpression(DataIndexes[0]);
			switch (Operation) {
				case CryptoNumOps.Add:
					context.Emit(new AssignmentStatement {
						Value = val + Key,
						Target = val
					});
					break;
				case CryptoNumOps.Xor:
					context.Emit(new AssignmentStatement {
						Value = val ^ Key,
						Target = val
					});
					break;
				case CryptoNumOps.Mul:
					context.Emit(new AssignmentStatement {
						Value = val * Key,
						Target = val
					});
					break;
				case CryptoNumOps.Xnor:
					context.Emit(new AssignmentStatement {
						Value = ~(val ^ Key),
						Target = val
					});
					break;
			}
		}

		public override void EmitInverse(CipherGenContext context) {
			var val = context.GetDataExpression(DataIndexes[0]);
			switch (Operation) {
				case CryptoNumOps.Add:
					context.Emit(new AssignmentStatement {
						Value = val - InverseKey,
						Target = val
					});
					break;
				case CryptoNumOps.Xor:
					context.Emit(new AssignmentStatement {
						Value = val ^ InverseKey,
						Target = val
					});
					break;
				case CryptoNumOps.Mul:
					context.Emit(new AssignmentStatement {
						Value = val * InverseKey,
						Target = val
					});
					break;
				case CryptoNumOps.Xnor:
					context.Emit(new AssignmentStatement {
						Value = val ^ InverseKey,
						Target = val
					});
					break;
			}
		}
	}
}
