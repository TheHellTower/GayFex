using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex {
	internal static class MethodAnalyzer {
		internal static IEnumerable<MethodAnalyzerResult> GetRegexCalls(
			MethodDef method, IRegexTargetMethods moduleRegexMethods, ITraceService traceService) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(moduleRegexMethods != null, $"{nameof(moduleRegexMethods)} != null");
			Debug.Assert(traceService != null, $"{nameof(traceService)} != null");

			if (!method.HasBody || !method.Body.HasInstructions) yield break;

			IMethodTrace methodTrace = null;
			foreach (var instr in method.Body.Instructions) {
				if ((instr.OpCode == OpCodes.Newobj || instr.OpCode == OpCodes.Call) &&
				    instr.Operand is IMethod opMethod) {
					var regexMethod = moduleRegexMethods.GetMatchingMethod(opMethod);
					if (regexMethod != null) {
						if (methodTrace == null) methodTrace = traceService.Trace(method);

						var argumentInstr = methodTrace.TraceArguments(instr);

						// Check if tracing the method arguments was successful
						if (argumentInstr == null) continue;

						var result = new MethodAnalyzerResult {
							mainInstruction = instr,
							regexMethod = regexMethod
						};

						var patternInstr = method.Body.Instructions[argumentInstr[regexMethod.PatternParameterIndex]];
						if (patternInstr.OpCode != OpCodes.Ldstr) continue;
						result.patternInstr = patternInstr;

						var pattern = patternInstr.Operand as string;
						var options = RegexOptions.None;

						if (regexMethod.OptionsParameterIndex >= 0) {
							var optionsInstr =
								method.Body.Instructions[argumentInstr[regexMethod.OptionsParameterIndex]];
							if (optionsInstr.OpCode != OpCodes.Ldc_I4) continue;
							options = (RegexOptions)optionsInstr.Operand;

							if ((options & RegexOptions.Compiled) != 0) {
								options &= ~RegexOptions.Compiled;
								result.explicitCompiled = true;
							}
							else
								result.explicitCompiled = false;

							result.optionsInstr = optionsInstr;
						}
						else {
							result.explicitCompiled = false;
						}

						TimeSpan? timeout = null;
						bool staticTimeout = true;
						if (regexMethod.TimeoutParameterIndex >= 0) {
							staticTimeout = false;
							var timeoutInstr =
								method.Body.Instructions[argumentInstr[regexMethod.TimeoutParameterIndex]];
							var timeoutInstrs = ExtractTimespanFromCall(timeoutInstr, method, methodTrace, ref timeout,
								ref staticTimeout);
							result.timeoutInstrs = timeoutInstrs;
						}

						result.compileDef = new RegexCompileDef(pattern, options, timeout, staticTimeout);
						yield return result;
					}
				}
			}
		}

		private static IList<Instruction> ExtractTimespanFromCall(Instruction timeoutInstr, MethodDef method,
			IMethodTrace methodTrace, ref TimeSpan? timeout, ref bool staticTimeout) {
			Debug.Assert(timeoutInstr != null, $"{nameof(timeoutInstr)} != null");
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(methodTrace != null, $"{nameof(methodTrace)} != null");

			var instr = new List<Instruction>() {timeoutInstr};

			if ((timeoutInstr.OpCode == OpCodes.Call)
			    && (timeoutInstr.Operand is IMethod timespanCreateMethod)
			    && (timespanCreateMethod.DeclaringType.FullName == "System.TimeSpan")) {
				var creationMethod = typeof(TimeSpan).GetMethod(timespanCreateMethod.Name);
				if (creationMethod != null) {
					var timeoutParameters = methodTrace.TraceArguments(timeoutInstr);
					if (timeoutParameters.Length == 1) {
						var paramInstr = method.Body.Instructions[timeoutParameters[0]];
						if (paramInstr.OpCode == OpCodes.Ldc_R8) {
							instr.Add(paramInstr);
							timeout = (TimeSpan)creationMethod.Invoke(null, new[] {paramInstr.Operand});
							staticTimeout = true;
						}
					}
				}
			}

			return instr;
		}
	}
}
