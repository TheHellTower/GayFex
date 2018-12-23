using System;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Optimizations.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Optimizations.CompileRegex {
	internal sealed class InspectPhase : IProtectionPhase {
		internal InspectPhase(CompileRegexProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		internal CompileRegexProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		ProtectionTargets IProtectionPhase.Targets => ProtectionTargets.Modules;

		string IProtectionPhase.Name => "Inspect modules";

		bool IProtectionPhase.ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var regexService = context.Registry.GetRequiredService<ICompileRegexService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(CompileRegexProtection.Id);

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				logger.LogMsgInspectingModule(module);

				if (regexService.AnalyzeModule(module))
					logger.LogMsgRegexReferencesFound(module);
				else
					logger.LogMsgNoRegexReferencesFound(module);

				token.ThrowIfCancellationRequested();
			}
		}
	}
}
