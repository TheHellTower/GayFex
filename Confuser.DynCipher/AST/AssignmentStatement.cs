namespace Confuser.DynCipher.AST {
	public class AssignmentStatement : Statement {
		public Expression Target { get; set; }
		public Expression Value { get; set; }

		public override string ToString() => $"{Target} = {Value};";
	}
}
