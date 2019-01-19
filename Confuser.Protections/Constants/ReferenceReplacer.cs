using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Helpers;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Confuser.Protections.Constants {
	internal static class ReferenceReplacer {
		internal static bool ReplaceReference(ConstantProtection protection, CEContext ctx, IProtectionParameters parameters) {
			foreach (var entry in ctx.ReferenceRepl) {
				if (parameters.GetParameter(ctx.Context, entry.Key, protection.Parameters.ControlFlowGraphReplacement)) {
					if (!ReplaceCFG(entry.Key, entry.Value, ctx)) return false;
				}
				else {
					if (!ReplaceNormal(entry.Key, entry.Value)) return false;
				}
			}
			return true;
		}

		private static bool ReplaceNormal(MethodDef method, IEnumerable<(Instruction TargetInstruction, uint Argument, IMethod DecoderMethod)> instrs) {
			foreach (var (targetInstruction, argument, decoderMethod) in instrs) {
				Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_I4 || decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Int32);
				Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_I8 || decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Int64);
				Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_R4 || decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Single);
				Debug.Assert(targetInstruction.OpCode != OpCodes.Ldc_R8 || decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.Double);
				Debug.Assert(targetInstruction.OpCode != OpCodes.Ldstr || decoderMethod.GetReturnTypeSig() == method.Module.CorLibTypes.String);

				int i = method.Body.Instructions.IndexOf(targetInstruction);
				targetInstruction.OpCode = OpCodes.Ldc_I4;
				targetInstruction.Operand = (int)argument;

				method.Body.Instructions.Insert(i + 1, OpCodes.Call.ToInstruction(decoderMethod));
			}
			return true;
		}

		private static TypeSig GetReturnTypeSig(this IMethod method) {
			if (method.MethodSig.RetType.IsGenericParameter) {
				var genericReturn = (GenericSig)method.MethodSig.RetType;
				var genericMethod = (MethodSpec)method;
				return ((GenericInstMethodSig)genericMethod.Instantiation).GenericArguments[(int)genericReturn.Number];
			}

			return method.MethodSig.RetType;
		}

		struct CFGContext {
			public CEContext Ctx;
			public ControlFlowGraph Graph;
			public BlockKey[] Keys;
			public IRandomGenerator Random;
			public Dictionary<uint, CFGState> StatesMap;
			public Local StateVariable;
		}

		struct CFGState {
			public uint A;
			public uint B;
			public uint C;
			public uint D;

			public CFGState(uint seed) {
				A = seed *= 0x21412321;
				B = seed *= 0x21412321;
				C = seed *= 0x21412321;
				D = seed *= 0x21412321;
			}

			public void UpdateExplicit(int id, uint value) {
				switch (id) {
					case 0:
						A = value;
						break;
					case 1:
						B = value;
						break;
					case 2:
						C = value;
						break;
					case 3:
						D = value;
						break;
				}
			}

			public void UpdateIncremental(int id, uint value) {
				switch (id) {
					case 0:
						A *= value;
						break;
					case 1:
						B += value;
						break;
					case 2:
						C ^= value;
						break;
					case 3:
						D -= value;
						break;
				}
			}

			public uint GetIncrementalUpdate(int id, uint target) {
				switch (id) {
					case 0:
						return A ^ target;
					case 1:
						return target - B;
					case 2:
						return C ^ target;
					case 3:
						return D - target;
				}
				throw new UnreachableException();
			}

			public uint Get(int id) {
				switch (id) {
					case 0:
						return A;
					case 1:
						return B;
					case 2:
						return C;
					case 3:
						return D;
				}
				throw new UnreachableException();
			}

			public static byte EncodeFlag(bool exp, int updateId, int getId) {
				byte fl = (byte)(exp ? 0x80 : 0);
				fl |= (byte)updateId;
				fl |= (byte)(getId << 2);
				return fl;
			}
		}

		private static bool InjectStateType(CEContext ctx) {
			if (ctx.CfgCtxType == null) {
				var rtType = GetRuntimeType("Confuser.Runtime.CFGCtx", ctx);
				if (rtType == null) return false;

				var injectResult = InjectHelper.Inject(rtType, ctx.Module,
					InjectBehaviors.RenameAndInternalizeBehavior(ctx.Context));

				ctx.CfgCtxType = injectResult.Requested.Mapped;
				ctx.CfgCtxCtor = injectResult.Where(inj => inj.Source.IsMethodDef && ((MethodDef)inj.Source).IsInstanceConstructor).Single().Mapped as MethodDef;
				ctx.CfgCtxNext = injectResult.Where(inj => inj.Source.IsMethodDef && inj.Source.Name.Equals("Next")).Single().Mapped as MethodDef;

				foreach (var def in injectResult)
					ctx.Name?.MarkHelper(ctx.Context, def.Mapped, ctx.Marker, ctx.Protection);
			}
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

		static void InsertEmptyStateUpdate(CFGContext ctx, ControlFlowBlock block) {
			var body = ctx.Graph.Body;
			var key = ctx.Keys[block.Id];
			if (key.EntryState == key.ExitState)
				return;

			Instruction first = null;
			// Cannot use graph.IndexOf because instructions has been modified.
			int targetIndex = body.Instructions.IndexOf(block.Header);

			CFGState entry;
			if (!ctx.StatesMap.TryGetValue(key.EntryState, out entry)) {
				key.Type = BlockKeyType.Explicit;
			}


			if (key.Type == BlockKeyType.Incremental) {
				// Incremental

				CFGState exit;
				if (!ctx.StatesMap.TryGetValue(key.ExitState, out exit)) {
					// Create new exit state
					// Update one of the entry states to be exit state
					exit = entry;
					int updateId = ctx.Random.NextInt32(3);
					uint targetValue = ctx.Random.NextUInt32();
					exit.UpdateExplicit(updateId, targetValue);

					int getId = ctx.Random.NextInt32(3);
					var fl = CFGState.EncodeFlag(false, updateId, getId);
					var incr = entry.GetIncrementalUpdate(updateId, targetValue);

					body.Instructions.Insert(targetIndex++, first = Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
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
						var fl = CFGState.EncodeFlag(false, stateId, getId);
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

				CFGState exit;
				if (!ctx.StatesMap.TryGetValue(key.ExitState, out exit)) {
					// Create new exit state from random seed
					var seed = ctx.Random.NextUInt32();
					exit = new CFGState(seed);
					body.Instructions.Insert(targetIndex++, first = Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
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
						var fl = CFGState.EncodeFlag(true, stateId, getId);

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

		static uint InsertStateGetAndUpdate(CFGContext ctx, ref int index, BlockKeyType type, ref CFGState currentState, CFGState? targetState) {
			var body = ctx.Graph.Body;

			if (type == BlockKeyType.Incremental) {
				// Incremental

				if (targetState == null) {
					// Randomly update and get state
					int updateId = ctx.Random.NextInt32(3);
					uint targetValue = ctx.Random.NextUInt32();

					int getId = ctx.Random.NextInt32(3);
					var fl = CFGState.EncodeFlag(false, updateId, getId);
					var incr = currentState.GetIncrementalUpdate(updateId, targetValue);
					currentState.UpdateExplicit(updateId, targetValue);

					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));

					return currentState.Get(getId);
				}
				// Scan for updated state
				int[] stateIds = { 0, 1, 2, 3 };
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
					var fl = CFGState.EncodeFlag(false, stateId, getId);
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
					currentState = new CFGState(seed);
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, ctx.StateVariable));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Dup));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)seed));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxCtor));

					// Randomly get state
					int updateId = ctx.Random.NextInt32(3);
					uint targetValue = ctx.Random.NextUInt32();

					int getId = ctx.Random.NextInt32(3);
					var fl = CFGState.EncodeFlag(false, updateId, getId);
					var incr = currentState.GetIncrementalUpdate(updateId, targetValue);
					currentState.UpdateExplicit(updateId, targetValue);

					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)fl));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)incr));
					body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.Ctx.CfgCtxNext));

					return currentState.Get(getId);
				}
				else {
					// Scan for updated state
					int[] stateIds = { 0, 1, 2, 3 };
					ctx.Random.Shuffle(stateIds);
					int i = 0;
					uint getValue = 0;
					foreach (var stateId in stateIds) {
						uint targetValue = targetState.Value.Get(stateId);
						int getId = ctx.Random.NextInt32(3);
						var fl = CFGState.EncodeFlag(true, stateId, getId);
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

		private static bool ReplaceCFG(MethodDef method, List<(Instruction TargetInstruction, uint Argument, IMethod DecoderMethod)> instrs, CEContext ctx) {
			if (!InjectStateType(ctx)) return false;

			var graph = ControlFlowGraph.Construct(method.Body);
			var sequence = KeySequence.ComputeKeys(graph, null);

			var cfgCtx = new CFGContext {
				Ctx = ctx,
				Graph = graph,
				Keys = sequence,
				StatesMap = new Dictionary<uint, CFGState>(),
				Random = ctx.Random
			};

			cfgCtx.StateVariable = new Local(ctx.CfgCtxType.ToTypeSig());
			method.Body.Variables.Add(cfgCtx.StateVariable);
			method.Body.InitLocals = true;

			var blockReferences = new Dictionary<int, SortedList<int, (Instruction, uint, IMethod)>>();
			foreach (var instr in instrs) {
				var index = graph.IndexOf(instr.Item1);
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
				CFGState currentState;
				if (!cfgCtx.StatesMap.TryGetValue(key.EntryState, out currentState)) {
					Debug.Assert((graph[blockRef.Key].Type & ControlFlowBlockType.Entry) != 0);
					Debug.Assert(key.Type == BlockKeyType.Explicit);

					// Create new entry state
					uint blockSeed = ctx.Random.NextUInt32();
					currentState = new CFGState(blockSeed);
					cfgCtx.StatesMap[key.EntryState] = currentState;

					var index = graph.Body.Instructions.IndexOf(graph[blockRef.Key].Header);
					Instruction newHeader;
					method.Body.Instructions.Insert(index++, newHeader = Instruction.Create(OpCodes.Ldloca, cfgCtx.StateVariable));
					method.Body.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldc_I4, (int)blockSeed));
					method.Body.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, ctx.CfgCtxCtor));
					method.Body.ReplaceReference(graph[blockRef.Key].Header, newHeader);
					key.Type = BlockKeyType.Incremental;
				}
				var type = key.Type;

				for (int i = 0; i < blockRef.Value.Count; i++) {
					var refEntry = blockRef.Value.Values[i];

					CFGState? targetState = null;
					if (i == blockRef.Value.Count - 1) {
						CFGState exitState;
						if (cfgCtx.StatesMap.TryGetValue(key.ExitState, out exitState))
							targetState = exitState;
					}

					var index = graph.Body.Instructions.IndexOf(refEntry.Item1) + 1;
					var value = InsertStateGetAndUpdate(cfgCtx, ref index, type, ref currentState, targetState);

					refEntry.Item1.OpCode = OpCodes.Ldc_I4;
					refEntry.Item1.Operand = (int)(refEntry.Item2 ^ value);
					method.Body.Instructions.Insert(index++, Instruction.Create(OpCodes.Xor));
					method.Body.Instructions.Insert(index, Instruction.Create(OpCodes.Call, refEntry.Item3));

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
