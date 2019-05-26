using Confuser.Core.Services;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;

namespace Confuser.DynCipher {
	public interface IDynCipherService {
		void GenerateCipherPair(IRandomGenerator random, out StatementBlock encrypt, out StatementBlock decrypt);

		void GenerateExpressionPair(IRandomGenerator random, Expression var, Expression result, uint depth,
			out Expression expression, out Expression inverse);
	}

	internal class DynCipherService : IDynCipherService {
		public void GenerateCipherPair(IRandomGenerator random, out StatementBlock encrypt,
			out StatementBlock decrypt) {
			CipherGenerator.GeneratePair(random, out encrypt, out decrypt);
		}

		public void GenerateExpressionPair(IRandomGenerator random, Expression var, Expression result, uint depth,
			out Expression expression, out Expression inverse) {
			ExpressionGenerator.GeneratePair(random, var, result, depth, out expression, out inverse);
		}
	}
}
