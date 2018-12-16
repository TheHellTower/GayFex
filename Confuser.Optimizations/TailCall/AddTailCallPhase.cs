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
	internal sealed class AddTailCallPhase : IProtectionPhase {

		internal AddTailCallPhase(TailCallProtection parent) =>
			Parent = parent ?? throw new ArgumentNullException(nameof(parent));

		internal TailCallProtection Parent { get; }

		IConfuserComponent IProtectionPhase.Parent => Parent;

		public ProtectionTargets Targets => ProtectionTargets.Methods;

		public string Name => "Optimize tail call methods";

		public bool ProcessAll => false;

		public void Execute(IConfuserContext context, IProtectionParameters parameters, CancellationToken token) {
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (parameters == null) throw new ArgumentNullException(nameof(parameters));

			var logger = context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(TailCallProtection.Id);

			var modifiedMethods = 0;
			foreach (var method in parameters.Targets.OfType<MethodDef>())
				if (ProcessMethod(method, logger))
					modifiedMethods++;

			if (modifiedMethods > 0)
				logger.LogMsgTotalInjectedTailCalls(modifiedMethods);
		}

		/// <remarks>Internal for unit testing.</remarks>
		internal static bool ProcessMethod(MethodDef method, ILogger logger) {
			Debug.Assert(method != null, $"{nameof(method)} != null");

			if (method.HasBody && method.Body.HasInstructions) {
				logger?.LogMsgScanningForTailCall(method);

				var instructions = method.Body.Instructions;
				var instructionCount = instructions.Count;
				var modified = false;
				for (var i = 0; i < instructionCount; i++) {
					if (IsUnoptimizedTailCall(method, i)) {
						logger?.LogMsgFoundTailCallInMethod(method, instructions[i]);

						method.Body.InsertPrefixInstructions(instructions[i], Instruction.Create(OpCodes.Tailcall));
						i++;
						instructionCount++;
						if (instructions[i + 1].OpCode != OpCodes.Ret) {
							// This is likely a debug build. Lets insert a return and check for dead code later.
							instructions.Insert(i + 1, Instruction.Create(OpCodes.Ret));
							i++;
							instructionCount++;
						}
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

		private static bool IsUnoptimizedTailCall(MethodDef method, int i) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.HasBody, $"{nameof(method)}.HasBody");
			Debug.Assert(method.Body.HasInstructions, $"{nameof(method)}.Body.HasInstructions");
			Debug.Assert(i >= 0, $"{nameof(i)} >= 0");

			if (TailCallUtils.IsTailCall(method, i)) {
				return !(i > 1 && method.Body.Instructions[i - 1].OpCode == OpCodes.Tailcall);
			}
			return false;
		}
	}
}
