using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Optimizations.Services;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Optimizations.CompileRegex {
	internal sealed class CompilePhase : IProtectionPhase {
		internal CompilePhase(CompileRegexProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		internal CompileRegexProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		ProtectionTargets IProtectionPhase.Targets => ProtectionTargets.Modules;

		string IProtectionPhase.Name => "Compile regex";

		bool IProtectionPhase.ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var regexService = context.Registry.GetRequiredService<ICompileRegexService>();
			var regexService1 = regexService as CompileRegexService;
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(CompileRegexProtection._Id);

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var expressions = regexService.GetExpressions(module).ToImmutableArray();

				logger.LogMsgRegexCompilingForModule(module, expressions.Length);

				var compiler = new Compiler.RegexCompiler(module) {
					ExpectedExpressions = expressions.Length
				};

				foreach (var expression in expressions) {
					var result = compiler.Compile(expression);
					regexService1?.AddCompiledRegex(module, result);

					logger.LogMsgRegexFinishedCompiling(result);
					token.ThrowIfCancellationRequested();
				}
			}
		}
	}
}
