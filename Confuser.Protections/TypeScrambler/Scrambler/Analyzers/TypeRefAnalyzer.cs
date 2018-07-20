using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble.Scrambler.Analyzers {
	class TypeRefAnalyzer : ContextAnalyzer<TypeRef> {
		public override void Process(ScannedMethod m, TypeRef o) {
			m.RegisterGeneric(o.ToTypeSig());
		}
	}
}
