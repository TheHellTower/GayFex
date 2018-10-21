using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed partial class RegexRunnerGoMethodCompiler : RegexRunnerMethodCompiler {
		private readonly IList<BacktrackNote> _notes;
		private readonly IDictionary<int, int> _goto;
		private readonly IDictionary<int, int> _uniquenote;

		private readonly IDictionary<int, RegexMethodCompilerLabel> _codePosLabel;
		private RegexMethodCompilerLabel _backwardLabel;

		internal RegexMethodCompilerLabel BackwardLabel {
			get {
				if (_backwardLabel == null) {
					_backwardLabel = CreateLabel();
				}
				return _backwardLabel;
			}
		}

		internal bool CheckTimeout { get; set; } = true;

		internal RegexRunnerGoMethodCompiler(ModuleDef module, MethodDef method, RegexRunnerDef regexRunnerDef) : base(module, method, regexRunnerDef) {
			_notes = new List<BacktrackNote>();
			_goto = new Dictionary<int, int>();
			_uniquenote = new Dictionary<int, int>();

			_codePosLabel = new Dictionary<int, RegexMethodCompilerLabel>();
		}

		internal void GenerateGo(RegexOptions options, RegexCode code) {
			GenerateForwardSection(options, code);
			var afterForwardIndex = NextIndex();
			GenerateBacktrackSection(options, code);

			// The middle section is inserted later, because we need to know what cached fields are actually used.
			GenerateMiddleSection(afterForwardIndex);

			// Finishing the method will add the required initialization of the cache variables into the method.
			FinishMethod();
		}

		/// <summary>
		/// Generates the first section of MSIL. This section contains all the forward
		/// logic, and corresponds directory to the regex codes.
		///
		/// In the absence of backtracking, this is all we would need.
		/// </summary>
		internal void GenerateForwardSection(RegexOptions options, RegexCode code) {
			var codes = code.Codes;

			for (var codePos = 0; codePos < codes.Length; codePos += RegexCode.OpcodeSize(codes[codePos])) {
				MarkLabel(CodePosLabel(codePos));

				var regexOpCode = code.Codes[codePos];
				GenerateOneCode(options, code, regexOpCode, codePos, -1);
			}
		}

		/// <summary>
		/// Generates the middle section of the MSIL.This section contains the
		/// big switch jump that allows us to simulate a stack of addresses,
		/// and it also contains the calls that expand the tracking and the
		/// grouping stack when they get too full.
		/// </summary>
		internal void GenerateMiddleSection(int seekIndex) {
			Seek(seekIndex);
			// Backtrack switch
			MarkLabel(BackwardLabel);

			// first call EnsureStorage and update the local field caches
			UpdateCachedField(_regexRunnerDef.runtrackposFieldDef);
			UpdateCachedField(_regexRunnerDef.runstackposFieldDef);
			CallEnsureStorage();
			UpdateFieldCache(_regexRunnerDef.runtrackposFieldDef);
			UpdateFieldCache(_regexRunnerDef.runstackposFieldDef);
			UpdateFieldCache(_regexRunnerDef.runtrackFieldDef);
			UpdateFieldCache(_regexRunnerDef.runstackFieldDef);

			PopTrack();
			Add(Instruction.Create(OpCodes.Switch, _notes.Select(n => GetLabelInstruction(n._label)).ToArray()));

			SeekEnd();
		}

		/// <summary>
		/// Generates the last section of the MSIL. This section contains all of the backtracking logic.
		/// </summary>
		internal void GenerateBacktrackSection(RegexOptions options, RegexCode code) {
			for (var backtrackCodePos = 0; backtrackCodePos < _notes.Count; backtrackCodePos++) {
				var note = _notes[backtrackCodePos];
				if (note._flags != 0) {
					MarkLabel(note._label);

					var regexOpCode = code.Codes[note._codepos] | note._flags;
					GenerateOneCode(options, code, regexOpCode, note._codepos, backtrackCodePos);
				}
			}
		}

		internal void Back() => Br(BackwardLabel);

		#region Track
		/*
		 * Adds a backtrack note to the list of them, and returns the index of the new
		 * note (which is also the index for the jump used by the switch table)
		 */
		internal int AddBacktrackNote(int flags, RegexMethodCompilerLabel l, int codepos) {
			_notes.Add(new BacktrackNote(flags, l, codepos));
			return _notes.Count - 1;
		}

		/*
		 * Adds a backtrack note for the current operation; creates a new label for
		 * where the code will be, and returns the switch index.
		 */
		internal int AddTrack(int codePos) => AddTrack(RegexCode.Back, codePos);

		/*
		 * Adds a backtrack note for the current operation; creates a new label for
		 * where the code will be, and returns the switch index.
		 */
		internal int AddTrack(int flags, int codePos) => AddBacktrackNote(flags, CreateLabel(), codePos);

		/*
		 * Adds a switchtable entry for the specified position (for the forward
		 * logic; does not cause backtracking logic to be generated)
		 */
		internal int AddGoto(int destCodePos) {
			if (!_goto.TryGetValue(destCodePos, out var target)) {
				target = _goto[destCodePos] = AddBacktrackNote(0, CodePosLabel(destCodePos), destCodePos);
			}
			return target;
		}

		/*
		 * Adds a note for backtracking code that only needs to be generated once;
		 * if it's already marked to be generated, returns the switch index
		 * for the unique piece of code.
		 */
		internal int AddUniqueTrack(int id, int codePos) => AddUniqueTrack(id, codePos, RegexCode.Back);

		/*
		 * Adds a note for backtracking code that only needs to be generated once;
		 * if it's already marked to be generated, returns the switch index
		 * for the unique piece of code.
		 */
		internal int AddUniqueTrack(int id, int codePos, int flags) {
			if (!_uniquenote.TryGetValue(id, out var result)) {
				result = _uniquenote[id] = AddTrack(flags, codePos);
			}
			return result;
		}

		internal RegexMethodCompilerLabel CodePosLabel(int codePos) {
			if (!_codePosLabel.TryGetValue(codePos, out var label)) {
				label = _codePosLabel[codePos] = CreateLabel();
			}
			return label;
		}

		/*
		 * Prologue to code that will push an element on the tracking stack
		 */
		internal void ReadyPushTrack() {
			LdRunnerField(_regexRunnerDef.runtrackFieldDef);
			LdRunnerField(_regexRunnerDef.runtrackposFieldDef);
			Ldc(1);
			Sub();
			Dup();
			StRunnerField(_regexRunnerDef.runtrackposFieldDef);
		}

		/*
		 * Saves the value of a field.
		 */
		internal void PushTrack(FieldDef field) {
			ReadyPushTrack();
			LdRunnerField(field);
			DoPush();
		}
		internal void PushTrack(Local local) {
			ReadyPushTrack();
			Ldloc(local);
			DoPush();
		}

		/*
		 * Pops an element off the tracking stack (leave it on the operand stack)
		 */
		internal void PopTrack() {
			LdRunnerField(_regexRunnerDef.runtrackFieldDef);
			LdRunnerField(_regexRunnerDef.runtrackposFieldDef);
			Dup();
			Ldc(1);
			Add();
			StRunnerField(_regexRunnerDef.runtrackposFieldDef);
			DoPop();
		}

		/*
		 * Creates a backtrack note and pushes the switch index it on the tracking stack
		 */
		internal void Track(int codePos) {
			ReadyPushTrack();
			Ldc(AddTrack(codePos));
			DoPush();
		}

		internal void TrackUnique(int id, int codePos) {
			ReadyPushTrack();
			Ldc(AddUniqueTrack(id, codePos));
			DoPush();
		}

		internal void TrackUnique2(int id, int codePos) {
			ReadyPushTrack();
			Ldc(AddUniqueTrack(id, codePos, RegexCode.Back2));
			DoPush();
		}

		/*
		 * Retrieves the top entry on the tracking stack without popping
		 */
		internal void TopTrack() {
			LdRunnerField(_regexRunnerDef.runtrackFieldDef);
			LdRunnerField(_regexRunnerDef.runtrackposFieldDef);
			DoPeek();
		}

		/*
		 * Pushes the current switch index on the tracking stack so the backtracking
		 * logic will be repeated again next time we backtrack here.
		 */
		internal void Trackagain(int backtrackCodePos) {
			Debug.Assert(backtrackCodePos >= 0, $"{nameof(backtrackCodePos)} >= 0");

			ReadyPushTrack();
			Ldc(backtrackCodePos);
			DoPush();
		}

		#endregion

		#region Stack


		/*
		 * Saves the value of a local variable on the grouping stack
		 */
		internal void PushStack(FieldDef field) {
			ReadyPushStack();
			LdRunnerField(field);
			DoPush();
		}

		/*
		 * Saves the value of a local variable on the grouping stack
		 */
		internal void PushStack(Local local) {
			ReadyPushStack();
			Ldloc(local);
			DoPush();
		}

		/*
		 * Prologue to code that will replace the ith element on the grouping stack
		 */
		internal void ReadyReplaceStack(int i) {
			LdRunnerField(_regexRunnerDef.runstackFieldDef);
			LdRunnerField(_regexRunnerDef.runstackposFieldDef);

			if (i != 0) {
				Ldc(i);
				Add();
			}
		}

		/*
		 * Prologue to code that will push an element on the grouping stack
		 */
		internal void ReadyPushStack() {
			LdRunnerField(_regexRunnerDef.runstackFieldDef);
			LdRunnerField(_regexRunnerDef.runstackposFieldDef);
			Ldc(1);
			Sub();
			Dup();
			StRunnerField(_regexRunnerDef.runstackposFieldDef);
		}

		/*
		 * Retrieves the top entry on the stack without popping
		 */
		internal void TopStack() {
			LdRunnerField(_regexRunnerDef.runstackFieldDef);
			LdRunnerField(_regexRunnerDef.runstackposFieldDef);
			DoPop();
		}

		/*
		 * Pops an element off the grouping stack (leave it on the operand stack)
		 */
		internal void PopStack() {
			LdRunnerField(_regexRunnerDef.runstackFieldDef);
			LdRunnerField(_regexRunnerDef.runstackposFieldDef);
			Dup();
			Ldc(1);
			Add();
			StRunnerField(_regexRunnerDef.runstackposFieldDef);
			DoPop();
		}

		/*
		 * Pops 1 element off the grouping stack and discards it
		 */
		internal void PopDiscardStack() => PopDiscardStack(1);

		/*
		 * Pops i elements off the grouping stack and discards them
		 */
		internal void PopDiscardStack(int i) {
			LdRunnerField(_regexRunnerDef.runstackposFieldDef);
			Ldc(i);
			Add();
			StRunnerField(_regexRunnerDef.runstackposFieldDef);
		}
		#endregion

		/*
		 * Epilogue to code that will push an element on a stack (use Ld* in between)
		 */
		internal void DoPush() => Add(new Instruction(OpCodes.Stelem_I4));

		/*
		 * Epilogue to code that will replace an element on a stack (use Ld* in between)
		 */
		internal void DoReplace() => Add(new Instruction(OpCodes.Stelem_I4));

		internal void DoPop() => Add(new Instruction(OpCodes.Ldelem_I4));
		internal void DoPeek() => Add(new Instruction(OpCodes.Ldelem_I4));

		/*
		 * Branch to the MSIL corresponding to the regex code at i
		 *
		 * A trick: since track and stack space is gobbled up unboundedly
		 * only as a result of branching backwards, this is where we check
		 * for sufficient space and trigger reallocations.
		 *
		 * If the "goto" is backwards, we generate code that checks
		 * available space against the amount of space that would be needed
		 * in the worst case by code that will only go forward; if there's
		 * not enough, we push the destination on the tracking stack, then
		 * we jump to the place where we invoke the allocator.
		 *
		 * Since forward gotos pose no threat, they just turn into a Br.
		 */
		internal void Goto(RegexCode code, int targetCodePos, int codePos) {
			if (targetCodePos < codePos) {
				var l1 = CreateLabel();

				// When going backwards, ensure enough space.
				LdRunnerField(_regexRunnerDef.runtrackposFieldDef);
				Ldc(code.TrackCount * 4);
				Ble(l1);
				LdRunnerField(_regexRunnerDef.runstackposFieldDef);
				Ldc(code.TrackCount * 3);
				Bgt(CodePosLabel(targetCodePos));
				MarkLabel(l1);
				ReadyPushTrack();
				Ldc(AddGoto(targetCodePos));
				DoPush();
				Br(BackwardLabel);
			}
			else {
				Br(CodePosLabel(targetCodePos));
			}
		}

		/*
		 * Goto the next (forward) operation
		 */
		internal void Advance(RegexCode code, int codePos) => Br(AdvanceLabel(code, codePos));

		/*
		 * The label for the next (forward) operation
		 */
		internal RegexMethodCompilerLabel AdvanceLabel(RegexCode code, int codePos) => CodePosLabel(NextCodepos(code, codePos));

		/*
		 * Returns the position of the next operation in the regex code, taking
		 * into account the different numbers of arguments taken by operations
		 */
		internal static int NextCodepos(RegexCode code, int codePos) =>
			codePos + RegexCode.OpcodeSize(code.Codes[codePos]);


		/*
		 * Keeps track of an operation that needs to be referenced in the backtrack-jump
		 * switch table, and that needs backtracking code to be emitted (if flags != 0)
		 */
		private sealed class BacktrackNote {
			internal readonly int _codepos;
			internal readonly int _flags;
			internal readonly RegexMethodCompilerLabel _label;

			internal BacktrackNote(int flags, RegexMethodCompilerLabel label, int codepos) {
				_codepos = codepos;
				_flags = flags;
				_label = label;
			}
		}
	}
}
