using System;

namespace Confuser.DynCipher.AST {
	public class UnaryOpExpression : Expression {
		public Expression Value { get; set; }
		public UnaryOps Operation { get; set; }

		public UnaryOpExpression() { }

		public UnaryOpExpression(UnaryOps operation, Expression value) {
			Operation = operation;
			Value = value ?? throw new ArgumentNullException(nameof(value));
		}

		public override string ToString() {
			string op;
			switch (Operation) {
				case UnaryOps.Not:
					op = "~";
					break;
				case UnaryOps.Negate:
					op = "-";
					break;
				default:
					op = "?";
					break;
			}

			return op + Value;
		}
	}
}
