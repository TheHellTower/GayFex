using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Optimizations.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Optimizations.CompileRegex {

	internal sealed class ExtractPhase : IProtectionPhase {

		internal ExtractPhase(CompileRegexProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		internal CompileRegexProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		ProtectionTargets IProtectionPhase.Targets => ProtectionTargets.Methods;

		string IProtectionPhase.Name => "Extract Regex phase";

		bool IProtectionPhase.ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var regexService = context.Registry.GetRequiredService<ICompileRegexService>();
			var traceService = context.Registry.GetRequiredService<ITraceService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(CompileRegexProtection._Id);

			foreach (var modulesAndMethods in parameters.Targets.OfType<MethodDef>().ToLookup(m => m.Module)) {
				var moduleRegexMethods = regexService.GetRegexTargetMethods(modulesAndMethods.Key);
				if (moduleRegexMethods == null) continue;


				foreach (var method in modulesAndMethods) {
					logger.LogMsgExtractFromMethod(method);

					var onlyExplicit = parameters.GetParameter(context, method, Parent.Parameters.OnlyCompiled);

					foreach (var result in MethodAnalyzer.GetRegexCalls(method, moduleRegexMethods, traceService)) {
						logger.LogMsgFoundRegexReferenceInMethod(method, result.regexMethod);

						if (!onlyExplicit || result.explicitCompiled) {
							regexService.RecordExpression(modulesAndMethods.Key, result.compileDef, result.regexMethod);
						} else {
							logger.LogMsgSkippedRegexNotCompiled(method);
						}
					}
					token.ThrowIfCancellationRequested();
				}
			}
		}
	}
}
