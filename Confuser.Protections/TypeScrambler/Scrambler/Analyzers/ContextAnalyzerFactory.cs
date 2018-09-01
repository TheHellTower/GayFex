using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
	internal sealed class ContextAnalyzerFactory : IEnumerable<ContextAnalyzer> {
		private IDictionary<Type, ContextAnalyzer> Analyzers { get; } = new Dictionary<Type, ContextAnalyzer>();
		private ScannedMethod TargetMethod { get; }
		internal ContextAnalyzerFactory(ScannedMethod method) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			TargetMethod = method;
		}

		internal void Add(ContextAnalyzer a) {
			Analyzers.Add(a.TargetType(), a);
		}

		internal void Analyze(Instruction inst) {
			Debug.Assert(inst != null, $"{nameof(inst)} != null");
			Debug.Assert(inst.Operand != null, $"{nameof(inst)}.Operand != null");

			var operand = inst.Operand;
			var analyzerRefType = operand.GetType().BaseType;
			
			if (Analyzers.TryGetValue(analyzerRefType, out var analyzer))
				analyzer.ProcessOperand(TargetMethod, inst, operand);
		}

		public IEnumerator<ContextAnalyzer> GetEnumerator() => Analyzers.Values.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
