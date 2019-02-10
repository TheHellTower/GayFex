using System;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
	internal abstract class ContextAnalyzer {
		public abstract Type TargetType();

		public abstract void ProcessOperand(ScannedMethod m, object o);
	}


	internal abstract class ContextAnalyzer<T> : ContextAnalyzer {
		public override Type TargetType() => typeof(T);
		public abstract void Process(ScannedMethod m, T o);

		public override void ProcessOperand(ScannedMethod m, object o) {
			Process(m, (T)o);
		}
	}
}
