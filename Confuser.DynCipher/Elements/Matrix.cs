using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher.Elements {
	internal class Matrix : CryptoElement {
		public Matrix()
			: base(4) {
		}

		private ReadOnlyMemory<uint> Key { get; set; }
		private ReadOnlyMemory<uint> InverseKey { get; set; }

		[Pure]
        [DebuggerStepThrough]
		private static int M4(int col, int row) => row * 4 + col;

		[Pure]
		[DebuggerStepThrough]
		private static int M3(int col, int row) => row * 3 + col;

		private static void GenerateUnimodularMatrix(IRandomGenerator random, Span<uint> result) {
			uint Next() => random.NextUInt32(4);

			Span<uint> lower = stackalloc uint[] {
				1, 0, 0, 0,
				Next(), 1, 0, 0,
				Next(), Next(), 1, 0,
				Next(), Next(), Next(), 1
			};

			Span<uint> upper = stackalloc uint[] {
				1, Next(), Next(), Next(),
				0, 1, Next(), Next(),
				0, 0, 1, Next(),
				0, 0, 0, 1
			};

			Multiply4(lower, upper, result);
		}

		private static void Multiply4(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> result) {
			for (int i = 0; i < 4; i++)
			for (int j = 0; j < 4; j++) {
				result[M4(i, j)] = 0;
				for (int k = 0; k < 4; k++)
					result[M4(i, j)] += left[M4(i, k)] * right[M4(k, j)];
			}
		}

		private static uint Cofactor4(ReadOnlySpan<uint> mat, int i, int j) {
			Span<uint> sub = stackalloc uint[9];
			for (int ci = 0, si = 0; ci < 4; ci++, si++) {
				if (ci == i) {
					si--;
					continue;
				}

				for (int cj = 0, sj = 0; cj < 4; cj++, sj++) {
					if (cj == j) {
						sj--;
						continue;
					}

					sub[M3(si, sj)] = mat[M4(ci, cj)];
				}
			}

			var ret = Determinant3(sub);
			if ((i + j) % 2 == 0) return ret;
			return (uint) -ret;
		}

		private static uint Determinant3(ReadOnlySpan<uint> mat) =>
			mat[M3(0, 0)] * mat[M3(1, 1)] * mat[M3(2, 2)] +
			mat[M3(0, 1)] * mat[M3(1, 2)] * mat[M3(2, 0)] +
			mat[M3(0, 2)] * mat[M3(1, 0)] * mat[M3(2, 1)] -
			mat[M3(0, 2)] * mat[M3(1, 1)] * mat[M3(2, 0)] -
			mat[M3(0, 1)] * mat[M3(1, 0)] * mat[M3(2, 2)] -
			mat[M3(0, 0)] * mat[M3(1, 2)] * mat[M3(2, 1)];

		private static void Transpose4(Span<uint> mat) {
			Span<uint> temp = stackalloc uint[16];
			Transpose4(mat, temp);
			temp.CopyTo(mat);
		}

		private static void Transpose4(ReadOnlySpan<uint> mat, Span<uint> result) {
			for (int i = 0; i < 4; i++)
			for (int j = 0; j < 4; j++)
				result[M4(j, i)] = mat[M4(i, j)];
		}

		public override void Initialize(IRandomGenerator random) {
			Span<uint> mat1 = stackalloc uint[16];
			Span<uint> mat2 = stackalloc uint[16];
			GenerateUnimodularMatrix(random, mat1);
			Transpose4(mat1);
			GenerateUnimodularMatrix(random, mat2);

			Memory<uint> invKey = new uint[16];
			Multiply4(mat1, mat2, invKey.Span);
			InverseKey = invKey;

			Memory<uint> key = new uint[16];
			for (int i = 0; i < 4; i++)
			for (int j = 0; j < 4; j++)
				key.Span[M4(i, j)] = Cofactor4(invKey.Span, i, j);
			Transpose4(key.Span);
			Key = key;
		}

		private void EmitCore(CipherGenContext context, ReadOnlySpan<uint> k) {
			var a = context.GetDataExpression(DataIndexes[0]);
			var b = context.GetDataExpression(DataIndexes[1]);
			var c = context.GetDataExpression(DataIndexes[2]);
			var d = context.GetDataExpression(DataIndexes[3]);

			using (context.AcquireTempVar(out var ta))
			using (context.AcquireTempVar(out var tb))
			using (context.AcquireTempVar(out var tc))
			using (context.AcquireTempVar(out var td))
				context.Emit(new AssignmentStatement {
						Value = a * k[M4(0, 0)] + b * k[M4(0, 1)] + c * k[M4(0, 2)] + d * k[M4(0, 3)],
						Target = ta
					}).Emit(new AssignmentStatement {
						Value = a * k[M4(1, 0)] + b * k[M4(1, 1)] + c * k[M4(1, 2)] + d * k[M4(1, 3)],
						Target = tb
					}).Emit(new AssignmentStatement {
						Value = a * k[M4(2, 0)] + b * k[M4(2, 1)] + c * k[M4(2, 2)] + d * k[M4(2, 3)],
						Target = tc
					}).Emit(new AssignmentStatement {
						Value = a * k[M4(3, 0)] + b * k[M4(3, 1)] + c * k[M4(3, 2)] + d * k[M4(3, 3)],
						Target = td
					})
					.Emit(new AssignmentStatement { Value = ta, Target = a })
					.Emit(new AssignmentStatement { Value = tb, Target = b })
					.Emit(new AssignmentStatement { Value = tc, Target = c })
					.Emit(new AssignmentStatement { Value = td, Target = d });
		}

		public override void Emit(CipherGenContext context) => EmitCore(context, Key.Span);

		public override void EmitInverse(CipherGenContext context) => EmitCore(context, InverseKey.Span);
	}
}
