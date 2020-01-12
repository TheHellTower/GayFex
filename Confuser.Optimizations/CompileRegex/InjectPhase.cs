using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Optimizations.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Optimizations.CompileRegex {
	internal sealed class InjectPhase : IProtectionPhase {
		internal InjectPhase(CompileRegexProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		internal CompileRegexProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		ProtectionTargets IProtectionPhase.Targets => ProtectionTargets.Methods;

		string IProtectionPhase.Name => "Inject regex";

		bool IProtectionPhase.ProcessAll => false;

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters,
			CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var regexService = context.Registry.GetRequiredService<ICompileRegexService>();
			var regexService1 = regexService as CompileRegexService;
			var traceService = context.Registry.GetRequiredService<ITraceService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(CompileRegexProtection.Id);

			if (regexService1 == null) throw new InvalidOperationException("Unexpected implementation of CompileRegexService");

			foreach (var method in parameters.Targets.OfType<MethodDef>()) {
				var moduleRegexMethods = regexService.GetRegexTargetMethods(method.Module);
				if (moduleRegexMethods == null) continue;

				// .ToArray is required because the instructions are modified.
				foreach (var result in MethodAnalyzer.GetRegexCalls(method, moduleRegexMethods, traceService)
					.ToArray()) {
					var compileResult = regexService1.GetCompiledRegex(method.Module, result.CompileDef);
					if (compileResult == null) continue;

					MethodDef newMethod;
					if (result.RegexMethod.InstanceEquivalentMethod == null)
						newMethod = result.CompileDef.StaticTimeout
							? compileResult.CreateMethod
							: compileResult.CreateWithTimeoutMethod;
					else
						compileResult.StaticHelperMethods.TryGetValue(result.RegexMethod, out newMethod);

					if (newMethod == null) {
						logger.LogMsgNoMatchingTargetMethod(result, compileResult);
						continue;
					}

					method.Body.RemoveInstruction(result.PatternInstruction);
					if (result.OptionsInstruction != null)
						method.Body.RemoveInstruction(result.OptionsInstruction);
					if (result.CompileDef.StaticTimeout && result.TimeoutInstructions != null)
						foreach (var timeoutInstr in result.TimeoutInstructions)
							method.Body.RemoveInstruction(timeoutInstr);

					Debug.Assert(newMethod != null, $"{nameof(newMethod)} != null");
					Debug.Assert(method.Body.Instructions.Contains(result.MainInstruction),
						"Method does not contain main instruction?");

					result.MainInstruction.OpCode = OpCodes.Call;
					result.MainInstruction.Operand = newMethod;
					logger.LogMsgInjectSuccessful(compileResult, method);
				}

				token.ThrowIfCancellationRequested();
			}
		}
	}
}
