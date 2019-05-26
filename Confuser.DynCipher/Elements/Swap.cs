using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class Swap : CryptoElement {
		public Swap()
			: base(2) {
		}

		public uint Mask { get; private set; }
		public uint Key { get; private set; }

		public override void Initialize(IRandomGenerator random) {
			Mask = random.NextInt32(3) == 0 ? 0xffffffff : random.NextUInt32();
			Key = random.NextUInt32() | 1;
		}

		private void EmitCore(CipherGenContext context) {
			var a = context.GetDataExpression(DataIndexes[0]);
			var b = context.GetDataExpression(DataIndexes[1]);
			VariableExpression tmp;

			if (Mask == 0xffffffff) {
				/*  t = a * k;
					a = b;
					b = t * k^-1;
				 */
				using (context.AcquireTempVar(out tmp)) {
					context.Emit(new AssignmentStatement {
						Value = a * Key,
						Target = tmp
					}).Emit(new AssignmentStatement {
						Value = b,
						Target = a
					}).Emit(new AssignmentStatement {
						Value = tmp * MathsUtils.ModInv(Key),
						Target = b
					});
				}
			}
			else {
				var mask = (LiteralExpression)Mask;
				var notMask = (LiteralExpression)~Mask;
				/*  t = (a & mask) * k;
					a = a & (~mask) | (b & mask);
					b = b & (~mask) | (t * k^-1);
				 */
				using (context.AcquireTempVar(out tmp)) {
					context.Emit(new AssignmentStatement {
						Value = (a & mask) * Key,
						Target = tmp
					}).Emit(new AssignmentStatement {
						Value = (a & notMask) | (b & mask),
						Target = a
					}).Emit(new AssignmentStatement {
						Value = (b & notMask) | (tmp * MathsUtils.ModInv(Key)),
						Target = b
					});
				}
			}
		}

		public override void Emit(CipherGenContext context) {
			EmitCore(context);
		}

		public override void EmitInverse(CipherGenContext context) {
			EmitCore(context);
		}
	}
}
