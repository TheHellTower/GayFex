using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Elements;
using Confuser.DynCipher.Transforms;

namespace Confuser.DynCipher.Generation {
	internal class CipherGenerator {
		private const int MAT_RATIO = 4;
		private const int NUMOP_RATIO = 10;
		private const int SWAP_RATIO = 6;
		private const int BINOP_RATIO = 9;
		private const int ROTATE_RATIO = 6;
		private const int RATIO_SUM = MAT_RATIO + NUMOP_RATIO + SWAP_RATIO + BINOP_RATIO + ROTATE_RATIO;
		private const double VARIANCE = 0.2;

		private static void PostProcessStatements(StatementBlock block, IRandomGenerator random) {
			MulToShiftTransform.Run(block);
			NormalizeBinOpTransform.Run(block);
			ExpansionTransform.Run(block);
			ShuffleTransform.Run(block, random);
			ConvertVariables.Run(block);
		}

		public static void GeneratePair(IRandomGenerator random, out StatementBlock encrypt,
			out StatementBlock decrypt) {
			double varPrecentage = 1 + ((random.NextDouble() * 2) - 1) * VARIANCE;
			var totalElements = (int)(((random.NextDouble() + 1) * RATIO_SUM) * varPrecentage);

			var elems = new List<CryptoElement>();
			for (int i = 0; i < totalElements * MAT_RATIO / RATIO_SUM; i++)
				elems.Add(new Matrix());
			for (int i = 0; i < totalElements * NUMOP_RATIO / RATIO_SUM; i++)
				elems.Add(new NumOp());
			for (int i = 0; i < totalElements * SWAP_RATIO / RATIO_SUM; i++)
				elems.Add(new Swap());
			for (int i = 0; i < totalElements * BINOP_RATIO / RATIO_SUM; i++)
				elems.Add(new BinOp());
			for (int i = 0; i < totalElements * ROTATE_RATIO / RATIO_SUM; i++)
				elems.Add(new RotateBit());
			for (int i = 0; i < 16; i++)
				elems.Add(new AddKey(i));
			random.Shuffle(elems);


			var x = Enumerable.Range(0, 16).ToArray();
			int index = 16;
			bool overdue = false;
			foreach (var elem in elems) {
				elem.Initialize(random);
				for (int i = 0; i < elem.DataCount; i++) {
					if (index == 16) {
						overdue = true; // Can't shuffle now to prevent duplication
						index = 0;
					}

					elem.DataIndexes[i] = x[index++];
				}

				if (overdue) {
					random.Shuffle(x);
					index = 0;
					overdue = false;
				}
			}

			var encryptContext = new CipherGenContext(random, 16);
			foreach (var elem in elems)
				elem.Emit(encryptContext);
			encrypt = encryptContext.Block;
			PostProcessStatements(encrypt, random);


			var decryptContext = new CipherGenContext(random, 16);
			foreach (var elem in Enumerable.Reverse(elems))
				elem.EmitInverse(decryptContext);
			decrypt = decryptContext.Block;
			PostProcessStatements(decrypt, random);
		}
	}
}
