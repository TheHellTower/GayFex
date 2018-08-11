using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Protections.Services;
using Confuser.Protections.TypeScramble.Scrambler;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;

namespace Confuser.Protections.TypeScramble {
	internal sealed class AnalyzePhase : IProtectionPhase {

		public AnalyzePhase(TypeScrambleProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public TypeScrambleProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Types | ProtectionTargets.Methods;

		public bool ProcessAll => false;

		public string Name => "Type scanner";

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			var logger = context.Registry.GetRequiredService<ILoggingService>().GetLogger("typescramble");
			//CreateGenericsForTypes(context, parameters.Targets.OfType<TypeDef>().WithProgress(context.Logger), token);

			CreateGenericsForMethods(context, parameters.Targets.OfType<MethodDef>()
				.OrderBy(x =>
				x?.Parameters?.Count ?? 0 +
				x.Body?.Variables?.Count ?? 0)
				.WithProgress(logger), token);
		}


		private void CreateGenericsForTypes(IConfuserContext context, IEnumerable<TypeDef> types, CancellationToken token) {
			var service = (TypeService)context.Registry.GetRequiredService<ITypeScrambleService>();

			foreach (var type in types) {
				if (type.Module.EntryPoint.DeclaringType != type) {
					service.AddScannedItem(new ScannedType(type));
				}
				token.ThrowIfCancellationRequested();
			}
		}

		private void CreateGenericsForMethods(IConfuserContext context, IEnumerable<MethodDef> methods, CancellationToken token) {
			var service = (TypeService)context.Registry.GetRequiredService<ITypeScrambleService>();

			foreach (var method in methods) {

				/*
				context.Logger.DebugFormat("[{0}]", method.Name);
				if (method.HasBody) {
					foreach(var i in method.Body.Instructions) {
						context.Logger.DebugFormat("{0} - {1} : {2}", i.OpCode, i?.Operand?.GetType().ToString() ?? "NULL", i.Operand);
					}
				}*/


				if (method.Module.EntryPoint != method && !(method.HasOverrides || method.IsAbstract || method.IsConstructor || method.IsGetter)) {
					service.AddScannedItem(new ScannedMethod(service, method));
				}
				token.ThrowIfCancellationRequested();
			}
		}

	}
}
