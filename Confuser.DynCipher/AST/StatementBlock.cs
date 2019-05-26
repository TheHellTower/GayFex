using System.Collections.Generic;
using System.Text;

namespace Confuser.DynCipher.AST {
	public class StatementBlock : Statement {
		public IList<Statement> Statements { get; } = new List<Statement>();

		public override string ToString() {
			var sb = new StringBuilder();
			sb.AppendLine("{");
			foreach (var i in Statements)
				sb.AppendLine(i.ToString());
			sb.AppendLine("}");
			return sb.ToString();
		}
	}
}
