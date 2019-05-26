using System;

namespace Confuser.DynCipher.AST {
	public class BinOpExpression : Expression {
		public Expression Left { get; set; }
		public Expression Right { get; set; }
		public BinOps Operation { get; set; }

		public BinOpExpression() {}

		public BinOpExpression(Expression left, BinOps operation, Expression right) {
			Left = left ?? throw new ArgumentNullException(nameof(left));
			Operation = operation;
			Right = right ?? throw new ArgumentNullException(nameof(right));
		}

		public override string ToString() {
			string op;
			switch (Operation) {
				case BinOps.Add:
					op = "+";
					break;
				case BinOps.Sub:
					op = "-";
					break;
				case BinOps.Div:
					op = "/";
					break;
				case BinOps.Mul:
					op = "*";
					break;
				case BinOps.Or:
					op = "|";
					break;
				case BinOps.And:
					op = "&";
					break;
				case BinOps.Xor:
					op = "^";
					break;
				case BinOps.Lsh:
					op = "<<";
					break;
				case BinOps.Rsh:
					op = ">>";
					break;
				default:
					op = "?";
					break;
			}

			return $"({Left} {op} {Right})";
		}
	}
}
