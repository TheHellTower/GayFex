using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble {
	class ScramblePhase : ProtectionPhase {

		public ScramblePhase(TypeScrambleProtection parent) : base(parent) {
		}

		public override ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods | ProtectionTargets.Modules;

		public override string Name => "Type scrambler";

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {

			var rewriter = new TypeRewriter(context);
			rewriter.ApplyGeterics();

			foreach (IDnlibDef def in parameters.Targets.WithProgress(context.Logger)) {

				switch (def) {

					case MethodDef md:
						if (md.HasBody) {
							rewriter.Process(md);
						}
						break;
					case ModuleDef mod:
						rewriter.ImportCode(mod);
						break;
				}

				context.CheckCancellation();
			}


		}
	}
}
