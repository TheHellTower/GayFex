namespace Confuser.DynCipher.AST {
	public abstract class Expression {
		public object Tag { get; set; }
		public abstract override string ToString();

		public static BinOpExpression Add(Expression a, Expression b) => new BinOpExpression(a, BinOps.Add, b);
		public static BinOpExpression Add(Expression a, uint b) => Add(a, new LiteralExpression(b));
		public static BinOpExpression Subtract(Expression a, Expression b) => new BinOpExpression(a, BinOps.Sub, b);
		public static BinOpExpression Subtract(Expression a, uint b) => Subtract(a, new LiteralExpression(b));
		public static BinOpExpression Multiply(Expression a, Expression b) => new BinOpExpression(a, BinOps.Mul, b);
		public static BinOpExpression Multiply(Expression a, uint b) => Multiply(a, new LiteralExpression(b));
		public static BinOpExpression RightShift(Expression a, int b) => new BinOpExpression(a, BinOps.Rsh, new LiteralExpression(b));
		public static BinOpExpression LeftShift(Expression a, int b) => new BinOpExpression(a, BinOps.Lsh, new LiteralExpression(b));
		public static BinOpExpression BitwiseOr(Expression a, Expression b) => new BinOpExpression(a, BinOps.Or, b);
		public static BinOpExpression BitwiseAnd(Expression a, Expression b) => new BinOpExpression(a, BinOps.And, b);
		public static BinOpExpression Xor(Expression a, Expression b) => new BinOpExpression(a, BinOps.Xor, b);
		public static BinOpExpression Xor(Expression a, uint b) => Xor(a, new LiteralExpression(b));
		public static UnaryOpExpression OnesComplement(Expression a) => new UnaryOpExpression(UnaryOps.Not, a);
		public static UnaryOpExpression Negate(Expression a) => new UnaryOpExpression(UnaryOps.Negate, a);

		public static BinOpExpression operator +(Expression a, Expression b) => Add(a, b);
		public static BinOpExpression operator +(Expression a, uint b) => Add(a, b);
		public static BinOpExpression operator -(Expression a, Expression b) => Subtract(a, b);
		public static BinOpExpression operator -(Expression a, uint b) => Subtract(a, b);
		public static BinOpExpression operator *(Expression a, Expression b) => Multiply(a, b);
		public static BinOpExpression operator *(Expression a, uint b) => Multiply(a, b);
		public static BinOpExpression operator >>(Expression a, int b) => RightShift(a, b);
		public static BinOpExpression operator <<(Expression a, int b) => LeftShift(a, b);
		public static BinOpExpression operator |(Expression a, Expression b) => BitwiseOr(a, b);
		public static BinOpExpression operator &(Expression a, Expression b) => BitwiseAnd(a, b);
		public static BinOpExpression operator ^(Expression a, Expression b) => Xor(a, b);
		public static BinOpExpression operator ^(Expression a, uint b) => Xor(a, b);
		public static UnaryOpExpression operator ~(Expression val) => OnesComplement(val);
		public static UnaryOpExpression operator -(Expression val) => Negate(val);
	}
}
