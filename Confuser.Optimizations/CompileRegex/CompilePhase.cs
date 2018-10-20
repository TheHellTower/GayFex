using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Optimizations.Services;
using Confuser.Renamer.Services;
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
			var markerService = context.Registry.GetRequiredService<IMarkerService>();
			var nameService = context.Registry.GetService<INameService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(CompileRegexProtection.Id);

			foreach (var module in parameters.Targets.OfType<ModuleDef>()) {
				var expressions = regexService.GetExpressions(module).ToImmutableArray();
				if (!expressions.Any()) continue;

				var skipBroken = parameters.GetParameter(context, module, Parent.Parameters.SkipBrokenExpressions);
				var skipUnsafe = parameters.GetParameter(context, module, Parent.Parameters.I18nSafeMode);

				logger.LogMsgRegexCompilingForModule(module, expressions.Length);

				var compiler = new Compiler.RegexCompiler(module) {
					ExpectedExpressions = expressions.Length
				};

				foreach (var expression in expressions) {
					if (skipUnsafe && Compiler.RegexCompiler.IsCultureUnsafe(expression)) {
						logger.LogMsgSkippedUnsafe(expression);
					}
					else {
						try {
							var result = compiler.Compile(expression);
							regexService1?.AddCompiledRegex(module, result);

							MarkType(result.RunnerTypeDef, context, markerService, nameService);
							MarkType(result.FactoryTypeDef, context, markerService, nameService);
							MarkType(result.RegexTypeDef, context, markerService, nameService);

							logger.LogMsgRegexFinishedCompiling(result);
							token.ThrowIfCancellationRequested();
						}
						catch (Compiler.RegexCompilerException ex) {
							if (skipBroken)
								logger.LogMsgRegexSkippedBrokenExpression(expression);
							else {
								logger.LogMsgInvalidRegexPatternFound(ex);
								throw;
							}
						}
					}
				}
			}
		}

		private void MarkType(TypeDef typeDef, IConfuserContext context, IMarkerService markerService, INameService nameService) {
			Debug.Assert(typeDef != null, $"{nameof(typeDef)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(markerService != null, $"{nameof(markerService)} != null");

			MarkDef(typeDef, context, markerService, nameService);

			foreach (var fieldDef in typeDef.Fields)
				MarkDef(fieldDef, context, markerService, nameService);
			foreach (var methodDef in typeDef.Methods)
				MarkDef(methodDef, context, markerService, nameService);
		}

		private void MarkDef(IDnlibDef def, IConfuserContext context, IMarkerService markerService, INameService nameService) {
			Debug.Assert(def != null, $"{nameof(def)} != null");
			Debug.Assert(context != null, $"{nameof(context)} != null");
			Debug.Assert(markerService != null, $"{nameof(markerService)} != null");

			if (nameService == null) {
				markerService.Mark(context, def, Parent);
			}
			else {
				nameService.MarkHelper(context, def, markerService, Parent);
			}
		}
	}
}
