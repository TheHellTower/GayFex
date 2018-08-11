using System;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Protections.TypeScramble.Scrambler;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScramble {
	internal sealed class ScramblePhase : IProtectionPhase {

		public ScramblePhase(TypeScrambleProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public TypeScrambleProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods | ProtectionTargets.Modules;

		public bool ProcessAll => false;

		public string Name => "Type scrambler";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var rewriter = new TypeRewriter(context);
			rewriter.ApplyGeterics();

			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("typescramble");

			foreach (var def in parameters.Targets.WithProgress(logger)) {
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

				token.ThrowIfCancellationRequested();
			}


		}
	}
}
