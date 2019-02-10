using System;
using System.Collections;
using System.Collections.Generic;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
	internal sealed class ContextAnalyzerFactory : IEnumerable {
		public Dictionary<Type, ContextAnalyzer> Analyzers = new Dictionary<Type, ContextAnalyzer>();
		private ScannedMethod targetMethod;

		public ContextAnalyzerFactory(ScannedMethod m) {
			targetMethod = m;
		}

		public void Add(ContextAnalyzer a) {
			Analyzers.Add(a.TargetType(), a);
		}

		public void Analyze(object o) {
			ContextAnalyzer a;
			Analyzers.TryGetValue(o.GetType().BaseType, out a);
			a?.ProcessOperand(targetMethod, o);
		}

		public IEnumerator GetEnumerator() {
			return Analyzers.Values.GetEnumerator();
		}
	}
}
