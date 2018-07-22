using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
	class MethodSpecAnalyzer : ContextAnalyzer<MethodSpec> {
		public override void Process(ScannedMethod m, MethodSpec o) {
			foreach (var t in o.GenericInstMethodSig.GenericArguments) {
				m.RegisterGeneric(t);
			}
		}
	}
}
