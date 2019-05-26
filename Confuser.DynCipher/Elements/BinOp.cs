using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class BinOp : CryptoElement {
		public BinOp() : base(2) {
		}

		public CryptoBinOps Operation { get; private set; }

		public override void Initialize(IRandomGenerator random) {
			Operation = (CryptoBinOps)random.NextInt32(3);
		}

		public override void Emit(CipherGenContext context) {
			var a = context.GetDataExpression(DataIndexes[0]);
			var b = context.GetDataExpression(DataIndexes[1]);
			switch (Operation) {
				case CryptoBinOps.Add:
					context.Emit(new AssignmentStatement {
						Value = a + b,
						Target = a
					});
					break;
				case CryptoBinOps.Xor:
					context.Emit(new AssignmentStatement {
						Value = a ^ b,
						Target = a
					});
					break;
				case CryptoBinOps.Xnor:
					context.Emit(new AssignmentStatement {
						Value = ~(a ^ b),
						Target = a
					});
					break;
			}
		}

		public override void EmitInverse(CipherGenContext context) {
			var a = context.GetDataExpression(DataIndexes[0]);
			var b = context.GetDataExpression(DataIndexes[1]);
			switch (Operation) {
				case CryptoBinOps.Add:
					context.Emit(new AssignmentStatement {
						Value = a - b,
						Target = a
					});
					break;
				case CryptoBinOps.Xor:
					context.Emit(new AssignmentStatement {
						Value = a ^ b,
						Target = a
					});
					break;
				case CryptoBinOps.Xnor:
					context.Emit(new AssignmentStatement {
						Value = a ^ (~b),
						Target = a
					});
					break;
			}
		}
	}
}
