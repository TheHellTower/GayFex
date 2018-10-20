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

		void IProtectionPhase.Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var regexService = context.Registry.GetRequiredService<ICompileRegexService>();
			var regexService1 = regexService as CompileRegexService;
			var traceService = context.Registry.GetRequiredService<ITraceService>();
			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(CompileRegexProtection.Id);

			Debug.Assert(regexService1 != null, $"{nameof(regexService1)} != null");
			if (regexService1 == null) return;

			foreach (var method in parameters.Targets.OfType<MethodDef>()) {
				var moduleRegexMethods = regexService.GetRegexTargetMethods(method.Module);
				if (moduleRegexMethods == null) continue;

				// .ToArray is required because the instructions are modified.
				foreach (var result in MethodAnalyzer.GetRegexCalls(method, moduleRegexMethods, traceService).ToArray()) {
					var compileResult = regexService1.GetCompiledRegex(method.Module, result.compileDef);
					if (compileResult == null) continue;

					MethodDef newMethod;
					if (result.regexMethod.InstanceEquivalentMethod == null) {
						if (result.compileDef.StaticTimeout) {
							newMethod = compileResult.CreateMethod;
						}
						else {
							newMethod = compileResult.CreateWithTimeoutMethod;
						}
					}
					else {
						compileResult.StaticHelperMethods.TryGetValue(result.regexMethod, out newMethod);
					}

					method.Body.RemoveInstruction(result.patternInstr);
					if (result.optionsInstr != null)
						method.Body.RemoveInstruction(result.optionsInstr);
					if (result.compileDef.StaticTimeout && result.timeoutInstrs != null) {
						foreach (var timeoutInstr in result.timeoutInstrs)
							method.Body.RemoveInstruction(timeoutInstr);
					}

					Debug.Assert(newMethod != null, $"{nameof(newMethod)} != null");

					result.mainInstruction.OpCode = OpCodes.Call;
					result.mainInstruction.Operand = newMethod;
				}
				token.ThrowIfCancellationRequested();
			}
		}
	}
}
