using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confuser.Core;
using Confuser.Core.Services;
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
			var trace = context.Registry.GetRequiredService<ITraceService>();

			var modifiedMethods = 0;
			foreach (var method in parameters.Targets.OfType<MethodDef>())
				if (ProcessMethod(method, logger, trace))
					modifiedMethods++;

			if (modifiedMethods > 0)
				logger.LogMsgTotalInjectedTailCalls(modifiedMethods);
		}

		/// <remarks>Internal for unit testing.</remarks>
		internal static bool ProcessMethod(MethodDef method, ILogger logger, ITraceService traceService) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(traceService != null, $"{nameof(traceService)} != null");

			if (!method.HasBody || !method.Body.HasInstructions) return false;

			using (logger?.LogBeginTailCallsScope(method)) {
				logger?.LogMsgScanningForTailCall(method);

				IMethodTrace trace = null;

				var instructions = method.Body.Instructions;
				var instructionCount = instructions.Count;
				var modified = false;
				for (var i = 0; i < instructionCount; i++) {
					if (trace == null) trace = traceService.Trace(method);
					if (!IsUnoptimizedTailCall(method, i, trace)) continue;

					logger?.LogMsgFoundTailCallInMethod(method, instructions[i]);

					method.Body.InsertPrefixInstructions(instructions[i], Instruction.Create(OpCodes.Tailcall));
					i++;
					instructionCount++;
					if (instructions[i + 1].OpCode != OpCodes.Ret) {
						// This is likely a debug build. Lets insert a return and check for dead code later.
						instructions.Insert(i + 1, Instruction.Create(OpCodes.Ret));
						i++;
						instructionCount++;
						trace = null; // Force the method trace to be initialized again (method body changed!)
					}

					modified = true;
				}

				if (modified) {
					TailCallUtils.RemoveUnreachableInstructions(method);
				}

				return modified;
			}
		}

		private static bool IsUnoptimizedTailCall(MethodDef method, int i, IMethodTrace trace) {
			Debug.Assert(method != null, $"{nameof(method)} != null");
			Debug.Assert(method.HasBody, $"{nameof(method)}.HasBody");
			Debug.Assert(method.Body.HasInstructions, $"{nameof(method)}.Body.HasInstructions");
			Debug.Assert(i >= 0, $"{nameof(i)} >= 0");
			Debug.Assert(trace != null, $"{nameof(trace)} != null");

			if (TailCallUtils.IsTailCall(method, i)) {
				var parameters = trace.TraceArguments(method.Body.Instructions[i]) ?? Array.Empty<int>();

				// Some instructions place a reference to a value on the stack. The Tailcall opcode can't handle those
				// so there is no reason to put a tail call in those calls.
				foreach (var pIndex in parameters) {
					var paramInstr = method.Body.Instructions[pIndex];
					switch (paramInstr.OpCode.Code) {
						case Code.Ldflda:
						case Code.Ldsflda:
						case Code.Ldelema:
						case Code.Ldarga:
						case Code.Ldarga_S:
						case Code.Ldloca:
						case Code.Ldloca_S:
							// These opcodes place a pointer on the stack. Tailcall aren't compatible with those.
							return false;
					}
				}

				return !(i > 1 && method.Body.Instructions[i - 1].OpCode == OpCodes.Tailcall);
			}

			return false;
		}
	}
}
