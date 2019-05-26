namespace Confuser.DynCipher.AST {
	public class ArrayIndexExpression : Expression {
		public Expression Array { get; set; }
		public int Index { get; set; }

		public override string ToString() => $"{Array}[{Index}]";
	}
}
