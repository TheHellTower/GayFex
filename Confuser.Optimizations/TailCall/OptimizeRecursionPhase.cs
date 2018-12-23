using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Confuser.Optimizations.TailCall {
	internal sealed class OptimizeRecursionPhase : IProtectionPhase {

		internal OptimizeRecursionPhase(TailCallProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		internal TailCallProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Optimize recursion";

		public bool ProcessAll => false;

		public void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(TailCallProtection.Id);

			var modifiedMethods = 0;
			foreach (var method in parameters.Targets.OfType<MethodDef>())
				if (parameters.GetParameter(context, method, Parent.Parameters.TailRecursion))
					if (ProcessMethod(method, logger))
						modifiedMethods++;

			if (modifiedMethods > 0)
				logger.LogMsgTotalInjectedTailRecursions(modifiedMethods);
		}

		/// <remarks>Internal for unit testing.</remarks>
		// ReSharper disable once MemberCanBePrivate.Global
		internal static bool ProcessMethod(MethodDef method, ILogger logger) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			// Fixing the recursion of methods is only safe for static methods,
			// because for virtual methods it may break the overwrites.
			if (!method.IsStatic) return false;

			logger?.LogMsgScanningForTailRecursion(method);

			if (method.HasBody && method.Body.HasInstructions) {
				var instructions = method.Body.Instructions;
				var instructionCount = instructions.Count;
				var modified = false;
				for (var i = 0; i < instructionCount; i++) {
					if (IsRecursiveTailCall(method, i)) {
						logger?.LogMsgFoundTailRecursionInMethod(method, instructions[i]);

						// So we do have a recursive tail call we can turn into a loop.
						// At the point of the method call, the variables required are on the stack. So we fill
						// the local parameters in reverse order.
						method.Body.InsertPrefixInstructions(instructions[i],
							method.Parameters.Reverse().Select(v => OpCodes.Starg.ToInstruction(v)));

						i += method.Parameters.Count;
						instructionCount += method.Parameters.Count;

						instructions[i].OpCode = OpCodes.Br;
						instructions[i].Operand = instructions[0];

						modified = true;
					}
				}

				if (modified) {
					TailCallUtils.RemoveUnreachableInstructions(method);
					return true;
				}
			}
			return false;
		}

		private static bool IsRecursiveTailCall(MethodDef method, int i) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.HasBody, $"{nameof(method)}.HasBody");
			Debug.Assert(method.Body.HasInstructions, $"{nameof(method)}.Body.HasInstructions");
			Debug.Assert(i >= 0, $"{nameof(i)} >= 0");

			if (TailCallUtils.IsTailCall(method, i)) {
				return method.Body.Instructions[i].Operand == method;
			}
			return false;
		}
	}
}
