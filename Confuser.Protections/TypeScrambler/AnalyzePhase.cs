using System;
using System.Diagnostics;
using System.Threading;
using Confuser.Core;
using Confuser.Protections.TypeScrambler.Scrambler;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScrambler {
	internal sealed class AnalyzePhase : IProtectionPhase {
		public AnalyzePhase(TypeScrambleProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public TypeScrambleProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods;

		public string Name => "Type scanner";
		
		public bool ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var typeService = context.Registry.GetRequiredService<TypeService>();

			foreach (var target in parameters.Targets /*.WithProgress(context.Logger)*/) {
				switch (target) {
					case TypeDef typeDef:
						typeService.AddScannedItem(new ScannedType(typeDef));
						break;
					case MethodDef methodDef:
						var scramblePublic = parameters.GetParameter(context, methodDef, Parent.Parameters.ScramblePublic);
						typeService.AddScannedItem(new ScannedMethod(typeService, methodDef, scramblePublic));
						break;
				}
				
				token.ThrowIfCancellationRequested();
			}
		}
	}
}
