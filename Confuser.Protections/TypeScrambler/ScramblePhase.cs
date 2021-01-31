using System;
using System.Threading;
using Confuser.Core;
using Confuser.Protections.TypeScrambler.Scrambler;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScrambler {
	internal sealed class ScramblePhase : IProtectionPhase {
		public ScramblePhase(TypeScrambleProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public TypeScrambleProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods;

		public string Name => "Type scrambling phase";
		
		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			// First check if the type scrambler did anything that needs rewriting.
			// If that is not the case, we can skip this whole thing.
			var service = context.Registry.GetRequiredService<TypeService>();
			if (!service.ScrambledAnything) return;

			var rewriter = new TypeRewriter(context);
			rewriter.ApplyGenerics();

			// In this stage the references to the scrambled types need to be fixed. This needs to be done for all
			// methods in the assembly, because all methods may contain references to the scrambled types and methods.
			foreach (var def in context.CurrentModule.FindDefinitions() /*.WithProgress(context.Logger)*/) {
				switch (def) {
					case MethodDef md:
						if (md.HasReturnType)
							md.ReturnType = rewriter.UpdateSignature(md.ReturnType);
						if (md.HasBody) {
							rewriter.ProcessBody(md);
						}
						break;
					case TypeDef td:
						foreach (var field in td.Fields) {
							field.FieldType = rewriter.UpdateSignature(field.FieldType);
						}
						break;
				}

				token.ThrowIfCancellationRequested();
			}
		}
	}
}
