using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Protections.Constants {
	internal static partial class ReferenceReplacer {
		private static bool InjectStateType(CEContext ctx) {
			if (ctx.CfgCtxType != null) return true;
			
			var rtType = GetRuntimeType("Confuser.Runtime.CFGCtx", ctx);
			if (rtType == null) return false;

			var injectHelper = ctx.Context.Registry.GetRequiredService<ProtectionsRuntimeService>().InjectHelper;

			var injectResult = injectHelper.Inject(rtType, ctx.Module,
				InjectBehaviors.RenameAndInternalizeBehavior(ctx.Context));

			ctx.CfgCtxType = injectResult.Requested.Mapped;
			ctx.CfgCtxCtor =
				injectResult
					.Single(inj => inj.Source.IsMethodDef && ((MethodDef)inj.Source).IsInstanceConstructor).Mapped as MethodDef;
			ctx.CfgCtxNext =
				injectResult.Single(inj => inj.Source.IsMethodDef && inj.Source.Name.Equals("Next"))
					.Mapped as MethodDef;

			foreach (var def in injectResult)
				ctx.Name?.MarkHelper(ctx.Context, def.Mapped, ctx.Marker, ctx.Protection);

			return true;
		}

		private static TypeDef GetRuntimeType(string fullName, CEContext ctx) {
			Debug.Assert(fullName != null, $"{nameof(fullName)} != null");
			Debug.Assert(ctx != null, $"{nameof(ctx)} != null");

			var logger = ctx.Context.Registry.GetRequiredService<ILoggerFactory>().CreateLogger(ConstantProtection._Id);
			var rt = ctx.Context.Registry.GetRequiredService<ProtectionsRuntimeService>().GetRuntimeModule();

			try {
				return rt.GetRuntimeType(fullName, ctx.Module);
			}
			catch (ArgumentException ex) {
				logger.LogError("Failed to load runtime: {0}", ex.Message);
			}

			return null;
		}

		private static void InsertEmptyStateUpdate(CfgContext ctx, ControlFlowBlock block) {
			var body = ctx.Graph.Body;
			var key = ctx.Keys[block.Id];
			if (key.EntryState == key.ExitState)
				return;

			Instruction first = null;
			// Cannot use graph.IndexOf because instructions has been modified.
			int targetIndex = body.Instructions.IndexOf(block.Header);

			if (!ctx.StatesMap.TryGetValue(key.EntryState, out var entry)) {
				key.Type = BlockKeyType.Explicit;
			}

			if (key.Type == BlockKeyType.Incremental) {
				// Incremental

				if (!ctx.StatesMap.TryGetValue(key.ExitState, out var exit)) {
					// Create new exit state
					// Update one of the entry states to be exit state
					exit = entry;
					int updateId = ctx.Random.NextInt32(3);
					uint targetValue = ctx.Random.NextUInt32();
					exit.UpdateExplicit(updateId, targetValue);

					int getId = ctx.Random.NextInt32(3);
					var fl = CfgState.EncodeFlag(false, updateId, getId);
					var incr = entry.GetIncrementalUpdate(updateId, targetValue);

					body.Instructions.Insert(targetIndex++,
						first = Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
					body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
					body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));
					body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Pop));

					ctx.StatesMap[key.ExitState] = exit;
				}
				else {
					// Scan for updated state
					var headerIndex = targetIndex;
					for (int stateId = 0; stateId < 4; stateId++) {
						if (entry.Get(stateId) == exit.Get(stateId))
							continue;

						uint targetValue = exit.Get(stateId);
						int getId = ctx.Random.NextInt32(3);
						var fl = CfgState.EncodeFlag(false, stateId, getId);
						var incr = entry.GetIncrementalUpdate(stateId, targetValue);

						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Pop));
					}

					first = body.Instructions[headerIndex];
				}
			}
			else {
				// Explicit

				if (!ctx.StatesMap.TryGetValue(key.ExitState, out var exit)) {
					// Create new exit state from random seed
					var seed = ctx.Random.NextUInt32();
					exit = new CfgState(seed);
					body.Instructions.Insert(targetIndex++,
						first = Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4, (int)seed));
					body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxCtor));

					ctx.StatesMap[key.ExitState] = exit;
				}
				else {
					// Scan for updated state
					var headerIndex = targetIndex;
					for (int stateId = 0; stateId < 4; stateId++) {
						uint targetValue = exit.Get(stateId);
						int getId = ctx.Random.NextInt32(3);
						var fl = CfgState.EncodeFlag(true, stateId, getId);

						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Ldc_I4, (int)targetValue));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));
						body.Instructions.Insert(targetIndex++, Instruction.Create(OpCodes.Pop));
					}

					first = body.Instructions[headerIndex];
				}
			}

			ctx.Graph.Body.ReplaceReference(block.Header, first);
		}

		private static uint InsertStateGetAndUpdate(CfgContext ctx, ref int index, BlockKeyType type, ref CfgState currentState,
			CfgState? targetState) {
			var body = ctx.Graph.Body;

			if (type == BlockKeyType.Incremental) {
				// Incremental

				if (targetState == null) {
					// Randomly update and get state
					int updateId = ctx.Random.NextInt32(3);
					uint targetValue = ctx.Random.NextUInt32();

					int getId = ctx.Random.NextInt32(3);
					var fl = CfgState.EncodeFlag(false, updateId, getId);
					var incr = currentState.GetIncrementalUpdate(updateId, targetValue);
					currentState.UpdateExplicit(updateId, targetValue);

					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));

					return currentState.Get(getId);
				}

				// Scan for updated state
				int[] stateIds = {0, 1, 2, 3};
				ctx.Random.Shuffle(stateIds);
				int i = 0;
				uint getValue = 0;
				foreach (var stateId in stateIds) {
					// There must be at least one update&get
					if (currentState.Get(stateId) == targetState.Value.Get(stateId) &&
					    i != stateIds.Length - 1) {
						i++;
						continue;
					}

					uint targetValue = targetState.Value.Get(stateId);
					int getId = ctx.Random.NextInt32(3);
					var fl = CfgState.EncodeFlag(false, stateId, getId);
					var incr = currentState.GetIncrementalUpdate(stateId, targetValue);
					currentState.UpdateExplicit(stateId, targetValue);

					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));

					i++;
					if (i == stateIds.Length)
						getValue = currentState.Get(getId);
					else
						body.Instructions.Insert(index++, Instruction.Create(OpCodes.Pop));
				}

				return getValue;
			}
			else {
				// Explicit

				if (targetState == null) {
					// Create new exit state from random seed
					var seed = ctx.Random.NextUInt32();
					currentState = new CfgState(seed);
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Dup));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)seed));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxCtor));

					// Randomly get state
					int updateId = ctx.Random.NextInt32(3);
					uint targetValue = ctx.Random.NextUInt32();

					int getId = ctx.Random.NextInt32(3);
					var fl = CfgState.EncodeFlag(false, updateId, getId);
					var incr = currentState.GetIncrementalUpdate(updateId, targetValue);
					currentState.UpdateExplicit(updateId, targetValue);

					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));

					return currentState.Get(getId);
				}
				else {
					// Scan for updated state
					int[] stateIds = {0, 1, 2, 3};
					ctx.Random.Shuffle(stateIds);
					int i = 0;
					uint getValue = 0;
					foreach (var stateId in stateIds) {
						uint targetValue = targetState.Value.Get(stateId);
						int getId = ctx.Random.NextInt32(3);
						var fl = CfgState.EncodeFlag(true, stateId, getId);
						currentState.UpdateExplicit(stateId, targetValue);

						body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
						body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
						body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)targetValue));
						body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));

						i++;
						if (i == stateIds.Length)
							getValue = targetState.Value.Get(getId);
						else
							body.Instructions.Insert(index++, Instruction.Create(OpCodes.Pop));
					}

					return getValue;
				}
			}
		}

		private static bool ReplaceCFG(MethodDef method,
			List<(Instruction TargetInstruction, uint Argument, IMethod DecoderMethod)> instrs, CEContext ctx) {
			if (!InjectStateType(ctx)) return false;

			var graph = ControlFlowGraph.Construct(method.Body);
			var sequence = KeySequence.ComputeKeys(graph, null);

			var cfgCtx = new CfgContext {
				Ctx = ctx,
				Graph = graph,
				Keys = sequence,
				StatesMap = new Dictionary<uint, CfgState>(),
				Random = ctx.Random,
				StateVariable = new Local(ctx.CfgCtxType.ToTypeSig())
			};

			method.Body.Variables.Add(cfgCtx.StateVariable);
			method.Body.InitLocals = true;

			var blockReferences = new Dictionary<int, SortedList<int, (Instruction TargetInstruction, uint Argument, IMethod DecoderMethod)>>();
			foreach (var instr in instrs) {
				var index = graph.IndexOf(instr.TargetInstruction);
				var block = graph.GetContainingBlock(index);

				if (!blockReferences.TryGetValue(block.Id, out var list))
					list = blockReferences[block.Id] = new SortedList<int, (Instruction, uint, IMethod)>();

				list.Add(index, instr);
			}

			// Update state for blocks not in use
			for (int i = 0; i < graph.Count; i++) {
				var block = graph[i];
				if (blockReferences.ContainsKey(block.Id))
					continue;
				InsertEmptyStateUpdate(cfgCtx, block);
			}

			// Update references
			foreach (var blockRef in blockReferences) {
				var key = sequence[blockRef.Key];
				if (!cfgCtx.StatesMap.TryGetValue(key.EntryState, out var currentState)) {
					Debug.Assert((graph[blockRef.Key].Type & ControlFlowBlockType.Entry) != 0);
					Debug.Assert(key.Type == BlockKeyType.Explicit);

					// Create new entry state
					uint blockSeed = ctx.Random.NextUInt32();
					currentState = new CfgState(blockSeed);
					cfgCtx.StatesMap[key.EntryState] = currentState;

					var index = graph.Body.Instructions.IndexOf(graph[blockRef.Key].Header);
					Instruction newHeader;
					method.Body.Instructions.Insert(index++,
						newHeader = Instruction.Create(OpCodes.Ldloca, cfgCtx.StateVariable));
					method.Body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)blockSeed));
					method.Body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.CfgCtxCtor));
					method.Body.ReplaceReference(graph[blockRef.Key].Header, newHeader);
					key.Type = BlockKeyType.Incremental;
				}

				var type = key.Type;

				for (int i = 0; i < blockRef.Value.Count; i++) {
					var refEntry = blockRef.Value.Values[i];

					CfgState? targetState = null;
					if (i == blockRef.Value.Count - 1) {
						if (cfgCtx.StatesMap.TryGetValue(key.ExitState, out var exitState))
							targetState = exitState;
					}
					
					var index = graph.Body.Instructions.IndexOf(refEntry.TargetInstruction);
					if (refEntry.TargetInstruction.OpCode.Code == Code.Call) {
						for (var idx = 0; idx < 4; idx++)
							graph.Body.RemoveInstruction(index - 4);
						index -= 4;
					}

					index++;

					var value = InsertStateGetAndUpdate(cfgCtx, ref index, type, ref currentState, targetState);

					refEntry.TargetInstruction.OpCode = OpCodes.Ldc_I4;
					refEntry.TargetInstruction.Operand = (int)(refEntry.Argument ^ value);
					method.Body.Instructions.Insert(index++, Instruction.Create(OpCodes.Xor));
					method.Body.Instructions.Insert(index, Instruction.Create(OpCodes.Call, refEntry.DecoderMethod));

					if (i == blockRef.Value.Count - 1 && targetState == null) {
						cfgCtx.StatesMap[key.ExitState] = currentState;
					}

					type = BlockKeyType.Incremental;
				}
			}

			return true;
		}
	}
}