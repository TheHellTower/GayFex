namespace Confuser.DynCipher.AST {
	public class LiteralExpression : Expression {
		public uint Value { get; set; }

		public LiteralExpression(uint val) => Value = val;
		public LiteralExpression(int val) => Value = unchecked((uint)val);

		public static LiteralExpression FromUInt32(uint val) => new LiteralExpression(val);
		public static LiteralExpression FromInt32(int val) => new LiteralExpression(val);

		public static implicit operator LiteralExpression(uint val) => FromUInt32(val);
		public static implicit operator LiteralExpression(int val) => FromInt32(val);

		public override string ToString() => $"{Value:x8}h";
	}
}
