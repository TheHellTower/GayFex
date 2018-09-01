using System;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Protections.TypeScramble.Scrambler;
using Confuser.Renamer;
using dnlib.DotNet;

namespace Confuser.Protections.TypeScramble {
	internal sealed class AnalyzePhase : ProtectionPhase {

		public AnalyzePhase(TypeScrambleProtection parent) : base(parent) {}

		public override ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods;

		public override string Name => "Type scanner";

		protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var typeService = context.Registry.GetService<TypeService>();
			Debug.Assert(typeService != null, $"{nameof(typeService)} != null");
			var nameService = context.Registry.GetService<INameService>();

			foreach (var target in parameters.Targets.WithProgress(context.Logger)) {
				switch (target) {
					case TypeDef typeDef:
						typeService.AddScannedItem(new ScannedType(typeDef, nameService));
						break;
					case MethodDef methodDef:
						var scramblePublic = parameters.GetParameter(context, methodDef, "scramblePublic", false);
						typeService.AddScannedItem(new ScannedMethod(typeService, nameService, methodDef, scramblePublic));
						break;

				}
				context.CheckCancellation();
			}
		}
	}
}
