using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Core.Services {
	/// <summary>
	///     The trace result of a method.
	/// </summary>
	internal sealed class MethodTrace : IMethodTrace {
		private Dictionary<int, List<Instruction>> _fromInstructions;
		private Dictionary<uint, int> _offset2Index;

		/// <summary>
		///     Initializes a new instance of the <see cref="MethodTrace" /> class.
		/// </summary>
		/// <param name="method">The method to trace.</param>
		internal MethodTrace(MethodDef method) => Method = method;

		/// <summary>
		///     Gets the method this trace belongs to.
		/// </summary>
		/// <value>The method.</value>
		private MethodDef Method { get; }

		/// <summary>
		///     Gets the instructions this trace is performed on.
		/// </summary>
		/// <value>The instructions.</value>
		private Instruction[] Instructions { get; set; }

		/// <summary>
		///     Gets the map of offset to index.
		/// </summary>
		/// <value>The map.</value>
		private Func<uint, int> OffsetToIndexMap => offset => _offset2Index[offset];

		/// <summary>
		///     Gets the stack depths of method body.
		/// </summary>
		/// <value>The stack depths.</value>
		private int[] BeforeStackDepths { get; set; }

		/// <summary>
		///     Perform the actual tracing.
		/// </summary>
		/// <returns>This instance.</returns>
		/// <exception cref="InvalidMethodException">Bad method body.</exception>
		internal MethodTrace Trace() {
			var body = Method.Body;
			Method.Body.UpdateInstructionOffsets();
			var instructions = Instructions = Method.Body.Instructions.ToArray();

			_offset2Index = new Dictionary<uint, int>();
			var beforeDepths = new int[instructions.Length];
			var afterDepths = new int[instructions.Length];
			_fromInstructions = new Dictionary<int, List<Instruction>>();

			for (int i = 0; i < instructions.Length; i++) {
				_offset2Index.Add(instructions[i].Offset, i);
				beforeDepths[i] = int.MinValue;
			}

			foreach (var eh in body.ExceptionHandlers) {
				beforeDepths[OffsetToIndexMap(eh.TryStart.Offset)] = 0;
				beforeDepths[OffsetToIndexMap(eh.HandlerStart.Offset)] =
					(eh.HandlerType != ExceptionHandlerType.Finally ? 1 : 0);
				if (eh.FilterStart != null)
					beforeDepths[OffsetToIndexMap(eh.FilterStart.Offset)] = 1;
			}

			// Just do a simple forward scan to build the stack depth map
			int currentStack = 0;
			for (int i = 0; i < instructions.Length; i++) {
				var instr = instructions[i];

				if (beforeDepths[i] != int.MinValue) // Already set due to being target of a branch / beginning of EHs.
					currentStack = beforeDepths[i];

				beforeDepths[i] = currentStack;
				instr.UpdateStack(ref currentStack);
				afterDepths[i] = currentStack;

				switch (instr.OpCode.FlowControl) {
					case FlowControl.Branch:
						int index = OffsetToIndexMap(((Instruction)instr.Operand).Offset);
						if (beforeDepths[index] == int.MinValue)
							beforeDepths[index] = currentStack;
						_fromInstructions.AddListEntry(OffsetToIndexMap(((Instruction)instr.Operand).Offset), instr);
						currentStack = 0;
						break;
					case FlowControl.Break:
						break;
					case FlowControl.Call:
						if (instr.OpCode.Code == Code.Jmp)
							currentStack = 0;
						break;
					case FlowControl.Cond_Branch:
						if (instr.OpCode.Code == Code.Switch)
							foreach (var target in (Instruction[])instr.Operand) {
								int targetIndex = OffsetToIndexMap(target.Offset);
								if (beforeDepths[targetIndex] == int.MinValue)
									beforeDepths[targetIndex] = currentStack;
								_fromInstructions.AddListEntry(OffsetToIndexMap(target.Offset), instr);
							}
						else {
							int targetIndex = OffsetToIndexMap(((Instruction)instr.Operand).Offset);
							if (beforeDepths[targetIndex] == int.MinValue)
								beforeDepths[targetIndex] = currentStack;
							_fromInstructions.AddListEntry(OffsetToIndexMap(((Instruction)instr.Operand).Offset), instr);
						}

						break;
					case FlowControl.Meta:
						break;
					case FlowControl.Next:
						break;
					case FlowControl.Return:
						break;
					case FlowControl.Throw:
						break;
					default:
						throw new UnreachableException();
				}
			}

			foreach (int stackDepth in beforeDepths)
				if (stackDepth == int.MinValue)
					throw new InvalidMethodException("Bad method body.");

			foreach (int stackDepth in afterDepths)
				if (stackDepth == int.MinValue)
					throw new InvalidMethodException("Bad method body.");

			BeforeStackDepths = beforeDepths;

			return this;
		}

		/// <summary>
		///     Traces the arguments of the specified call instruction.
		/// </summary>
		/// <param name="instr">The call instruction.</param>
		/// <returns>The indexes of the begin instruction of arguments.</returns>
		/// <exception cref="System.ArgumentException">The specified call instruction is invalid.</exception>
		/// <exception cref="InvalidMethodException">The method body is invalid.</exception>
		public int[] TraceArguments(Instruction instr) {
			if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt &&
				instr.OpCode.Code != Code.Newobj)
				throw new ArgumentException("Invalid call instruction.", nameof(instr));

			instr.CalculateStackUsage(pushes: out var push, out var pop); // pop is number of arguments
			if (pop == 0)
				return Array.Empty<int>();

			int instrIndex = OffsetToIndexMap(instr.Offset);
			int argCount = pop;
			int targetStack = BeforeStackDepths[instrIndex] - argCount;

			// Find the begin instruction of method call
			int beginInstrIndex = -1;
			var seen = new HashSet<uint>();
			var working = new Queue<int>();
			working.Enqueue(OffsetToIndexMap(instr.Offset) - 1);
			while (working.Count > 0) {
				int index = working.Dequeue();
				while (index >= 0) {
					if (BeforeStackDepths[index] == targetStack) {
						if (Method.Body.Instructions[index].OpCode.Code != Code.Dup) {
							// It's not a duplicate instruction, this is an acceptable start point.
							break;
						} else {
							var prevInstr = Method.Body.Instructions[index - 1];
							prevInstr.CalculateStackUsage(out push, out _);
							if (push > 0) {
								// A duplicate instruction is an acceptable start point in case the preceeding instruction
								// pushes a value.
								break;
							}
						}
					}

					if (_fromInstructions.ContainsKey(index))
						foreach (var fromInstr in _fromInstructions[index])
							if (!seen.Contains(fromInstr.Offset)) {
								seen.Add(fromInstr.Offset);
								working.Enqueue(OffsetToIndexMap(fromInstr.Offset));
							}

					index--;
				}

				if (index < 0)
					return null;

				if (beginInstrIndex == -1)
					beginInstrIndex = index;
				else if (beginInstrIndex != index)
					return null;
			}

			while (Instructions[beginInstrIndex].OpCode.Code == Code.Dup)
				beginInstrIndex--;

			// Trace the index of arguments
			seen.Clear();
			var working2 = new Queue<(int Index, Stack<int> EvalStack)>();
			working2.Clear();
			working2.Enqueue((beginInstrIndex, new Stack<int>()));
			int[] ret = null;
			while (working2.Count > 0) {
				var (index, evalStack) = working2.Dequeue();

				while (index != instrIndex && index < Instructions.Length) {
					var currentInstr = Instructions[index];

					currentInstr.CalculateStackUsage(out push, out pop);
					if (currentInstr.OpCode.Code == Code.Dup) {
						// Special case duplicate. This causes the current value on the stack to be duplicated.
						// To show this behaviour, we'll fetch the last object on the eval stack and add it back twice.
						Debug.Assert(pop == 1 && push == 2 && evalStack.Count > 0);
						var lastIdx = evalStack.Pop();
						evalStack.Push(lastIdx);
						evalStack.Push(lastIdx);
					}
					else {
						Debug.Assert(push <= 1); // Instructions shouldn't put more than one value on the stack.

						var diff = push - pop;
						if (diff < 0)
							for (var i = 0; i < -diff; i++)
								evalStack.Pop();
						else
							for (var i = 0; i < diff; i++)
								evalStack.Push(index);
					}

					switch (currentInstr.Operand) {
						case Instruction instruction: {
							int targetIndex = OffsetToIndexMap(instruction.Offset);
							if (currentInstr.OpCode.FlowControl == FlowControl.Branch)
								index = targetIndex;
							else {
								working2.Enqueue((targetIndex, CopyStack(evalStack)));
								index++;
							}

							break;
						}

						case Instruction[] targetInstructions: {
							foreach (var targetInstr in targetInstructions)
								working2.Enqueue((OffsetToIndexMap(targetInstr.Offset), CopyStack(evalStack)));
							index++;
							break;
						}

						default:
							index++;
							break;
					}
				}

				if (evalStack.Count > argCount) {
					// There are too many instructions on the eval stack.
					// That means that there are instructions for following commands.
					// To handle things properly we're only using the required amount on the top of the stack.
					var tmp = evalStack.ToArray();
					evalStack.Clear();
					foreach (var idx in tmp.Take(argCount).Reverse())
						evalStack.Push(idx);
				}

				if (evalStack.Count != argCount)
					return null;
				if (ret != null && !evalStack.SequenceEqual(ret))
					return null;
				ret = evalStack.ToArray();
			}

			if (ret == null)
				return null;

			Array.Reverse(ret);
			return ret;
		}

		public static Stack<T> CopyStack<T>(Stack<T> original)
		{
			var arr = new T[original.Count];
			original.CopyTo(arr, 0);
			Array.Reverse(arr);
			return new Stack<T>(arr);
		}
	}
}
