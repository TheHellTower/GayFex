using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core.Services;
using Confuser.DynCipher.AST;

namespace Confuser.DynCipher.Generation {
	internal class ExpressionGenerator {
		private static Expression GenerateExpression(IRandomGenerator random, Expression current, uint currentDepth,
			uint targetDepth) {
			if (currentDepth == targetDepth || (currentDepth > targetDepth / 3 && random.NextUInt32(100) > 85))
				return current;

			switch ((ExpressionOps)random.NextInt32(6)) {
				case ExpressionOps.Add:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) +
					       GenerateExpression(random, (LiteralExpression)random.NextUInt32(), currentDepth + 1,
						       targetDepth);

				case ExpressionOps.Sub:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) -
					       GenerateExpression(random, (LiteralExpression)random.NextUInt32(), currentDepth + 1,
						       targetDepth);

				case ExpressionOps.Mul:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) *
					       (LiteralExpression)(random.NextUInt32() | 1);

				case ExpressionOps.Xor:
					return GenerateExpression(random, current, currentDepth + 1, targetDepth) ^
					       GenerateExpression(random, (LiteralExpression)random.NextUInt32(), currentDepth + 1,
						       targetDepth);

				case ExpressionOps.Not:
					return ~GenerateExpression(random, current, currentDepth + 1, targetDepth);

				case ExpressionOps.Neg:
					return -GenerateExpression(random, current, currentDepth + 1, targetDepth);
			}

			throw new UnreachableException();
		}

		private static void SwapOperands(IRandomGenerator random, Expression exp) {
			switch (exp) {
				case BinOpExpression binExp: {
					if (random.NextBoolean()) {
						var tmp = binExp.Left;
						binExp.Left = binExp.Right;
						binExp.Right = tmp;
					}

					SwapOperands(random, binExp.Left);
					SwapOperands(random, binExp.Right);
					break;
				}

				case UnaryOpExpression unaryExp:
					SwapOperands(random, unaryExp.Value);
					break;
				case LiteralExpression _:
				case VariableExpression _:
					return;
				default:
					throw new UnreachableException();
			}
		}

		private static bool HasVariable(Expression exp, IDictionary<Expression, bool> hasVar) {
			if (hasVar.TryGetValue(exp, out var ret)) return ret;

			switch (exp) {
				case VariableExpression _:
					ret = true;
					break;
				case LiteralExpression _:
					ret = false;
					break;
				case BinOpExpression binExp: {
					ret = HasVariable(binExp.Left, hasVar) || HasVariable(binExp.Right, hasVar);
					break;
				}

				case UnaryOpExpression unaryExp:
					ret = HasVariable(unaryExp.Value, hasVar);
					break;
				default:
					throw new UnreachableException();
			}

			hasVar[exp] = ret;

			return ret;
		}

		private static Expression GenerateInverse(Expression exp, Expression var, Dictionary<Expression, bool> hasVar) {
			var result = var;
			while (!(exp is VariableExpression)) {
				Debug.Assert(hasVar[exp]);
				switch (exp) {
					case UnaryOpExpression unaryOp: {
						result = new UnaryOpExpression {
							Operation = unaryOp.Operation,
							Value = result
						};
						exp = unaryOp.Value;
						break;
					}

					case BinOpExpression binOp: {
						bool leftHasVar = hasVar[binOp.Left];
						var varExp = leftHasVar ? binOp.Left : binOp.Right;
						var constExp = leftHasVar ? binOp.Right : binOp.Left;

						switch (binOp.Operation) {
							case BinOps.Add:
								result = new BinOpExpression {
									Operation = BinOps.Sub,
									Left = result,
									Right = constExp
								};
								break;
							case BinOps.Sub when leftHasVar:
								// v - k = r => v = r + k
								result = new BinOpExpression {
									Operation = BinOps.Add,
									Left = result,
									Right = constExp
								};
								break;
							case BinOps.Sub:
								// k - v = r => v = k - r
								result = new BinOpExpression {
									Operation = BinOps.Sub,
									Left = constExp,
									Right = result
								};
								break;
							case BinOps.Mul: {
								Debug.Assert(constExp is LiteralExpression);
								uint val = ((LiteralExpression)constExp).Value;
								val = MathsUtils.ModInv(val);
								result = new BinOpExpression {
									Operation = BinOps.Mul,
									Left = result,
									Right = (LiteralExpression)val
								};
								break;
							}

							case BinOps.Xor:
								result = new BinOpExpression {
									Operation = BinOps.Xor,
									Left = result,
									Right = constExp
								};
								break;
						}

						exp = varExp;
						break;
					}
				}
			}

			return result;
		}

		public static void GeneratePair(IRandomGenerator random, Expression var, Expression result, uint depth,
			out Expression expression, out Expression inverse) {
			expression = GenerateExpression(random, var, 0, depth);
			SwapOperands(random, expression);

			var hasVar = new Dictionary<Expression, bool>();
			HasVariable(expression, hasVar);

			inverse = GenerateInverse(expression, result, hasVar);
		}

		private enum ExpressionOps {
			Add,
			Sub,
			Mul,
			Xor,
			Not,
			Neg
		}
	}
}
