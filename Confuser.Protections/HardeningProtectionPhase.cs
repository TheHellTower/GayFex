using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Confuser.Protections {
	internal sealed class HardeningProtectionPhase : IProtectionPhase {

		/// <inheritdoc />
		[SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
		public HardeningProtectionPhase(HardeningProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		public HardeningProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		/// <inheritdoc />
		public ProtectionTargets Targets => ProtectionTargets.Modules;

		/// <inheritdoc />
		public string Name => "Hardening Phase";

		/// <inheritdoc />
		public bool ProcessAll => false;

		/// <inheritdoc />
		public void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			foreach (var module in parameters.Targets.OfType<ModuleDef>())
				HardenMethod(context, module, token);
		}

		private static void HardenMethod(IConfuserContext context, ModuleDef module, CancellationToken token) {
			var cctor = module.GlobalType.FindStaticConstructor();
			if (cctor == null) {
				var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(HardeningProtection.Id);
				logger.LogDebug("No .cctor containing protection code found. Nothing to do.");
				return;
			}

			if (!cctor.HasBody || !cctor.Body.HasInstructions) return;

			var marker = context.Registry.GetRequiredService<IMarkerService>();
			var instructions = cctor.Body.Instructions;
			for (var i = instructions.Count - 1; i >= 0; i--) {
				token.ThrowIfCancellationRequested();

				if (instructions[i].OpCode.Code != Code.Call) continue;
				if (!(instructions[i].Operand is MethodDef targetMethod)) continue;
				if (!targetMethod.IsStatic || targetMethod.DeclaringType != module.GlobalType) continue;

				// Resource protection needs to rewrite the method during the write phase. Not compatible!
				if (!marker.IsMarked(context, targetMethod) || !(marker.GetHelperParent(targetMethod) is ResourceProtection)) continue;

				cctor.Body.MergeCall(instructions[i]);
				targetMethod.DeclaringType.Methods.Remove(targetMethod);
			}
		}
	}
}
