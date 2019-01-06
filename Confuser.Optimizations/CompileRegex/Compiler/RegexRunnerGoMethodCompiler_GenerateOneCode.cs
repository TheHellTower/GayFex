using System;
using System.Text.RegularExpressions;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal partial class RegexRunnerGoMethodCompiler {
		// indices for unique code fragments
		private const int stackpop = 0;    // pop one
		private const int stackpop2 = 1;    // pop two
		private const int capback = 3;    // uncapture
		private const int capback2 = 4;    // uncapture 2
		private const int branchmarkback2 = 5;    // back2 part of branchmark
		private const int lazybranchmarkback2 = 6;    // back2 part of lazybranchmark
		private const int branchcountback2 = 7;    // back2 part of branchcount
		private const int lazybranchcountback2 = 8;    // back2 part of lazybranchcount
		private const int forejumpback = 9;    // back part of forejump

		private void GenerateOneCode(RegexOptions options, RegexCode code, int regexOpCode, int codePos, int backtrackCodePos) {
			int Operand(int i) => code.Codes[codePos + i + 1];
			int Code = regexOpCode & RegexCode.Mask;
			bool IsRtl = (regexOpCode & RegexCode.Rtl) != 0;
			bool IsCi = (regexOpCode & RegexCode.Ci) != 0;

			if (CheckTimeout)
				CallCheckTimeout();

			switch (regexOpCode) {
				case RegexCode.Stop:
					//: return
					UpdateCachedField(_regexRunnerDef.runtextposFieldDef); //update the runtextpos field
					Ret();
					break;

				case RegexCode.Nothing:
					//: break Backward;
					Back();
					break;

				case RegexCode.Goto:
					//: Goto(Operand(0));
					Goto(code, Operand(0), codePos);
					break;

				case RegexCode.Testref:
					//: if (!_match.IsMatched(Operand(0)))
					//:     break Backward;
					CallIsMatched(Operand(0));
					Brfalse(BackwardLabel);
					break;

				case RegexCode.Lazybranch:
					//: Track(Textpos());
					PushTrack(_regexRunnerDef.runtextposFieldDef);
					Track(codePos);
					break;

				case RegexCode.Lazybranch | RegexCode.Back:
					//: Trackframe(1);
					//: Textto(Tracked(0));
					//: Goto(Operand(0));
					PopTrack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					Goto(code, Operand(0), codePos);
					break;

				case RegexCode.Nullmark:
					//: Stack(-1);
					//: Track();
					ReadyPushStack();
					Ldc(-1);
					DoPush();
					TrackUnique(stackpop, codePos);
					break;
				case RegexCode.Setmark:
					//: Stack(Textpos());
					//: Track();
					PushStack(_regexRunnerDef.runtextposFieldDef);
					TrackUnique(stackpop, codePos);
					break;

				case RegexCode.Nullmark | RegexCode.Back:
				case RegexCode.Setmark | RegexCode.Back:
					//: Stackframe(1);
					//: break Backward;
					PopDiscardStack();
					Back();
					break;
				case RegexCode.Getmark:
					//: Stackframe(1);
					//: Track(Stacked(0));
					//: Textto(Stacked(0));
					ReadyPushTrack();
					PopStack();
					Dup();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					DoPush();
					Track(codePos);
					break;

				case RegexCode.Getmark | RegexCode.Back:
					//: Trackframe(1);
					//: Stack(Tracked(0));
					//: break Backward;
					ReadyPushStack();
					PopTrack();
					DoPush();
					Back();
					break;

				case RegexCode.Capturemark: {
					//: if (!IsMatched(Operand(1)))
					//:     break Backward;
					//: Stackframe(1);
					//: if (Operand(1) != -1)
					//:     TransferCapture(Operand(0), Operand(1), Stacked(0), Textpos());
					//: else
					//:     Capture(Operand(0), Stacked(0), Textpos());
					//: Track(Stacked(0));

					//: Stackframe(1);
					//: Capture(Operand(0), Stacked(0), Textpos());
					//: Track(Stacked(0));

					var tempLocal = RequireLocalInt32();

					if (Operand(1) != -1) {
						CallIsMatched(Operand(1));
						Brfalse(BackwardLabel);
					}

					PopStack();
					Stloc(tempLocal);

					if (Operand(1) != -1)
						CallTransferCapture(Operand(0), Operand(1), tempLocal, _regexRunnerDef.runtextposFieldDef);
					else
						CallCapture(Operand(0), tempLocal, _regexRunnerDef.runtextposFieldDef);

					PushTrack(tempLocal);

					if (Operand(0) != -1 && Operand(1) != -1)
						TrackUnique(capback2, codePos);
					else
						TrackUnique(capback, codePos);

					FreeLocal(tempLocal);
					break;
				}
				case RegexCode.Capturemark | RegexCode.Back:
					//: Trackframe(1);
					//: Stack(Tracked(0));
					//: Uncapture();
					//: if (Operand(0) != -1 && Operand(1) != -1)
					//:     Uncapture();
					//: break Backward;
					ReadyPushStack();
					PopTrack();
					DoPush();
					CallUncapture();
					if (Operand(0) != -1 && Operand(1) != -1) {
						CallUncapture();
					}
					Back();
					break;

				case RegexCode.Branchmark: {
					//: Stackframe(1);
					//:
					//: if (Textpos() != Stacked(0))
					//: {                                   // Nonempty match -> loop now
					//:     Track(Stacked(0), Textpos());   // Save old mark, textpos
					//:     Stack(Textpos());               // Make new mark
					//:     Goto(Operand(0));               // Loop
					//: }
					//: else
					//: {                                   // Empty match -> straight now
					//:     Track2(Stacked(0));             // Save old mark
					//:     Advance(1);                     // Straight
					//: }
					//: continue Forward;
					var mark = RequireLocalInt32();
					var l1 = CreateLabel();

					PopStack();
					Dup();
					Stloc(mark);                            // Stacked(0) -> temp
					PushTrack(mark);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Beq(l1);                                // mark == textpos -> branch

					// (matched != 0)

					PushTrack(_regexRunnerDef.runtextposFieldDef);
					PushStack(_regexRunnerDef.runtextposFieldDef);
					Track(codePos);
					Goto(code, Operand(0), codePos);                       // Goto(Operand(0))

					// else

					MarkLabel(l1);
					TrackUnique2(branchmarkback2, codePos);

					FreeLocal(mark);
					break;
				}
				case RegexCode.Branchmark | RegexCode.Back:
					//: Trackframe(2);
					//: Stackframe(1);
					//: Textto(Tracked(1));                     // Recall position
					//: Track2(Tracked(0));                     // Save old mark
					//: Advance(1);
					PopTrack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					PopStack();
					Pop();
					// track spot 0 is already in place
					TrackUnique2(branchmarkback2, codePos);
					Advance(code, codePos);
					break;

				case RegexCode.Branchmark | RegexCode.Back2:
					//: Trackframe(1);
					//: Stack(Tracked(0));                      // Recall old mark
					//: break Backward;                         // Backtrack
					ReadyPushStack();
					PopTrack();
					DoPush();
					Back();
					break;

				case RegexCode.Lazybranchmark: {
					//: StackPop();
					//: int oldMarkPos = StackPeek();
					//:
					//: if (Textpos() != oldMarkPos) {         // Nonempty match -> next loop
					//: {                                   // Nonempty match -> next loop
					//:     if (oldMarkPos != -1)
					//:         Track(Stacked(0), Textpos());   // Save old mark, textpos
					//:     else
					//:         TrackPush(Textpos(), Textpos());
					//: }
					//: else
					//: {                                   // Empty match -> no loop
					//:     Track2(Stacked(0));             // Save old mark
					//: }
					//: Advance(1);
					//: continue Forward;
					var mark = RequireLocalInt32();
					var l1 = CreateLabel();
					var l2 = CreateLabel();
					var l3 = CreateLabel();

					PopStack();
					Dup();
					Stloc(mark);                      // Stacked(0) -> temp

					// if (oldMarkPos != -1)
					Ldloc(mark);
					Ldc(-1);
					Beq(l2);                                // mark == -1 -> branch
					PushTrack(mark);
					Br(l3);
					// else
					MarkLabel(l2);
					PushTrack(_regexRunnerDef.runtextposFieldDef);
					MarkLabel(l3);

					// if (Textpos() != mark)
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Beq(l1);                                // mark == textpos -> branch
					PushTrack(_regexRunnerDef.runtextposFieldDef);
					Track(codePos);
					Br(AdvanceLabel(code, codePos));                 // Advance (near)
																	 // else
					MarkLabel(l1);
					ReadyPushStack();                   // push the current textPos on the stack.
														// May be ignored by 'back2' or used by a true empty match.
					Ldloc(mark);

					DoPush();
					TrackUnique2(lazybranchmarkback2, codePos);

					FreeLocal(mark);

					break;
				}

				case RegexCode.Lazybranchmark | RegexCode.Back:
					//: Trackframe(2);
					//: Track2(Tracked(0));                     // Save old mark
					//: Stack(Textpos());                       // Make new mark
					//: Textto(Tracked(1));                     // Recall position
					//: Goto(Operand(0));                       // Loop

					PopTrack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					PushStack(_regexRunnerDef.runtextposFieldDef);
					TrackUnique2(lazybranchmarkback2, codePos);
					Goto(code, Operand(0), codePos);
					break;

				case RegexCode.Lazybranchmark | RegexCode.Back2:
					//: Stackframe(1);
					//: Trackframe(1);
					//: Stack(Tracked(0));                  // Recall old mark
					//: break Backward;
					ReadyReplaceStack(0);
					PopTrack();
					DoReplace();
					Back();
					break;

				case RegexCode.Nullcount:
					//: Stack(-1, Operand(0));
					//: Track();
					ReadyPushStack();
					Ldc(-1);
					DoPush();
					ReadyPushStack();
					Ldc(Operand(0));
					DoPush();
					TrackUnique(stackpop2, codePos);
					break;

				case RegexCode.Setcount:
					//: Stack(Textpos(), Operand(0));
					//: Track();
					PushStack(_regexRunnerDef.runtextposFieldDef);
					ReadyPushStack();
					Ldc(Operand(0));
					DoPush();
					TrackUnique(stackpop2, codePos);
					break;


				case RegexCode.Nullcount | RegexCode.Back:
				case RegexCode.Setcount | RegexCode.Back:
					//: Stackframe(2);
					//: break Backward;
					PopDiscardStack(2);
					Back();
					break;

				case RegexCode.Branchcount: {
					//: Stackframe(2);
					//: int mark = Stacked(0);
					//: int count = Stacked(1);
					//:
					//: if (count >= Operand(1) || Textpos() == mark && count >= 0)
					//: {                                   // Max loops or empty match -> straight now
					//:     Track2(mark, count);            // Save old mark, count
					//:     Advance(2);                     // Straight
					//: }
					//: else
					//: {                                   // Nonempty match -> count+loop now
					//:     Track(mark);                    // remember mark
					//:     Stack(Textpos(), count + 1);    // Make new mark, incr count
					//:     Goto(Operand(0));               // Loop
					//: }
					//: continue Forward;
					var count = RequireLocalInt32();
					var mark = RequireLocalInt32();
					var l1 = CreateLabel();
					var l2 = CreateLabel();

					PopStack();
					Stloc(count);                           // count -> temp
					PopStack();
					Dup();
					Stloc(mark);                            // mark -> temp2
					PushTrack(mark);

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Bne(l1);                                // mark != textpos -> l1
					Ldloc(count);
					Ldc(0);
					Bge(l2);                                // count >= 0 && mark == textpos -> l2

					MarkLabel(l1);
					Ldloc(count);
					Ldc(Operand(1));
					Bge(l2);                                // count >= Operand(1) -> l2

					// else
					PushStack(_regexRunnerDef.runtextposFieldDef);
					ReadyPushStack();
					Ldloc(count);                           // mark already on track
					Ldc(1);
					Add();
					DoPush();
					Track(codePos);
					Goto(code, Operand(0), codePos);

					// if (count >= Operand(1) || Textpos() == mark)
					MarkLabel(l2);
					PushTrack(count);                       // mark already on track
					TrackUnique2(branchcountback2, codePos);

					FreeLocal(count);
					FreeLocal(mark);
					break;
				}

				case RegexCode.Branchcount | RegexCode.Back: {
					//: Trackframe(1);
					//: Stackframe(2);
					//: if (Stacked(1) > 0)                     // Positive -> can go straight
					//: {
					//:     Textto(Stacked(0));                 // Zap to mark
					//:     Track2(Tracked(0), Stacked(1) - 1); // Save old mark, old count
					//:     Advance(2);                         // Straight
					//:     continue Forward;
					//: }
					//: Stack(Tracked(0), Stacked(1) - 1);      // recall old mark, old count
					//: break Backward;
					var count = RequireLocalInt32();
					var l1 = CreateLabel();

					PopStack();
					Ldc(1);
					Sub();
					Dup();
					Stloc(count);
					Ldc(0);
					Blt(l1);

					// if (count >= 0)
					PopStack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					PushTrack(count);                       // Tracked(0) is alredy on the track
					TrackUnique2(branchcountback2, codePos);
					Advance(code, codePos);

					// else
					MarkLabel(l1);
					ReadyReplaceStack(0);
					PopTrack();
					DoReplace();
					PushStack(count);
					Back();

					FreeLocal(count);
					break;
				}

				case RegexCode.Branchcount | RegexCode.Back2: {
					//: Trackframe(2);
					//: Stack(Tracked(0), Tracked(1));      // Recall old mark, old count
					//: break Backward;                     // Backtrack
					var tempV = RequireLocalInt32();

					PopTrack();
					Stloc(tempV);
					ReadyPushStack();
					PopTrack();
					DoPush();
					PushStack(tempV);
					Back();

					FreeLocal(tempV);
					break;
				}

				case RegexCode.Lazybranchcount: {
					//: Stackframe(2);
					//: int mark = Stacked(0);
					//: int count = Stacked(1);
					//:
					//: if (count < 0)
					//: {                                   // Negative count -> loop now
					//:     Track2(mark);                   // Save old mark
					//:     Stack(Textpos(), count + 1);    // Make new mark, incr count
					//:     Goto(Operand(0));               // Loop
					//: }
					//: else
					//: {                                   // Nonneg count or empty match -> straight now
					//:     Track(mark, count, Textpos());  // Save mark, count, position
					//: }
					var count = RequireLocalInt32();
					var mark = RequireLocalInt32();
					var l1 = CreateLabel();

					PopStack();
					Stloc(count);                           // count -> temp
					PopStack();
					Stloc(mark);                            // mark -> temp2

					Ldloc(count);
					Ldc(0);
					Bge(l1);                                // count >= 0 -> l1

					// if (count < 0)
					PushTrack(mark);
					PushStack(_regexRunnerDef.runtextposFieldDef);
					ReadyPushStack();
					Ldloc(count);
					Ldc(1);
					Add();
					DoPush();
					TrackUnique2(lazybranchcountback2, codePos);
					Goto(code, Operand(0), codePos);

					// else
					MarkLabel(l1);
					PushTrack(mark);
					PushTrack(count);
					PushTrack(_regexRunnerDef.runtextposFieldDef);
					Track(codePos);

					FreeLocal(count);
					FreeLocal(mark);
					break;
				}

				case RegexCode.Lazybranchcount | RegexCode.Back: {
					//: Trackframe(3);
					//: int mark = Tracked(0);
					//: int textpos = Tracked(2);
					//: if (Tracked(1) < Operand(1) && textpos != mark)
					//: {                                       // Under limit and not empty match -> loop
					//:     Textto(Tracked(2));                 // Recall position
					//:     Stack(Textpos(), Tracked(1) + 1);   // Make new mark, incr count
					//:     Track2(Tracked(0));                 // Save old mark
					//:     Goto(Operand(0));                   // Loop
					//:     continue Forward;
					//: }
					//: else
					//: {
					//:     Stack(Tracked(0), Tracked(1));      // Recall old mark, count
					//:     break Backward;                     // backtrack
					//: }
					var l1 = CreateLabel();
					var cV = RequireLocalInt32();

					PopTrack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					PopTrack();
					Dup();
					Stloc(cV);
					Ldc(Operand(1));
					Bge(l1);                                // Tracked(1) >= Operand(1) -> l1

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					TopTrack();
					Beq(l1);                                // textpos == mark -> l1

					PushStack(_regexRunnerDef.runtextposFieldDef);
					ReadyPushStack();
					Ldloc(cV);
					Ldc(1);
					Add();
					DoPush();
					TrackUnique2(lazybranchcountback2, codePos);
					Goto(code, Operand(0), codePos);

					MarkLabel(l1);
					ReadyPushStack();
					PopTrack();
					DoPush();
					PushStack(cV);
					Back();

					FreeLocal(cV);
					break;
				}

				case RegexCode.Lazybranchcount | RegexCode.Back2:
					//: TrackPop();
					//: StackPop(2);
					//: StackPush(TrackPeek(), StackPeek(1) - 1);   // Recall old mark, count
					//: break;                                      // Backtrack
					ReadyReplaceStack(1);
					PopTrack();
					DoReplace();
					ReadyReplaceStack(0);
					TopStack();
					Ldc(1);
					Sub();
					DoReplace();
					Back();
					break;

				case RegexCode.Setjump:
					//: Stack(Trackpos(), Crawlpos());
					//: Track();
					ReadyPushStack();

					// TODO: Verify: Original implementation did a forced load of the field and did not use the cache variable. Not sure why. It needs to be verified why the field was used.
					//Ldthisfld(_trackF);
					LdRunnerField(_regexRunnerDef.runtrackFieldDef);
					Ldlen();
					LdRunnerField(_regexRunnerDef.runtrackposFieldDef);
					Sub();
					DoPush();
					ReadyPushStack();
					CallCrawlpos();
					DoPush();
					TrackUnique(stackpop2, codePos);
					break;

				case RegexCode.Setjump | RegexCode.Back:
					//: Stackframe(2);
					PopDiscardStack(2);
					Back();
					break;

				case RegexCode.Backjump: {
					//: Stackframe(2);
					//: Trackto(Stacked(0));
					//: while (Crawlpos() != Stacked(1))
					//:     Uncapture();
					//: break Backward;

					PopStack();
					// TODO: Verify: Original implementation did a forced load of the field and did not use the cache variable. Not sure why. It needs to be verified why the field was used.
					//Ldthisfld(_trackF);
					LdRunnerField(_regexRunnerDef.runtrackFieldDef);
					Ldlen();
					PopStack();
					Sub();
					StRunnerField(_regexRunnerDef.runtrackposFieldDef);
					CrawlAndUncapture();
					break;
				}

				case RegexCode.Forejump: {
					//: Stackframe(2);
					//: Trackto(Stacked(0));
					//: Track(Stacked(1));
					var tempV = RequireLocalInt32();

					PopStack();
					Stloc(tempV);
					// TODO: Verify: Original implementation did a forced load of the field and did not use the cache variable. Not sure why. It needs to be verified why the field was used.
					//Ldthisfld(_trackF);
					LdRunnerField(_regexRunnerDef.runtrackFieldDef);
					Ldlen();
					PopStack();
					Sub();
					StRunnerField(_regexRunnerDef.runtrackposFieldDef);
					PushTrack(tempV);
					TrackUnique(forejumpback, codePos);

					FreeLocal(tempV);
					break;
				}

				case RegexCode.Forejump | RegexCode.Back: {
					//: Trackframe(1);
					//: while (Crawlpos() != Tracked(0))
					//:     Uncapture();
					//: break Backward;
					PopTrack();
					CrawlAndUncapture();
					break;
				}

				case RegexCode.Bol:
					//: if (Leftchars() > 0 && CharAt(Textpos() - 1) != '\n')
					//:     break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					Ble(CodePosLabel(NextCodepos(code, codePos)));
					Leftchar();
					Ldc('\n');
					Bne(BackwardLabel);
					break;

				case RegexCode.Eol:
					//: if (Rightchars() > 0 && CharAt(Textpos()) != '\n')
					//:     break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Bge(CodePosLabel(NextCodepos(code, codePos)));
					Rightchar();
					Ldc('\n');
					Bne(BackwardLabel);
					break;

				case RegexCode.Boundary:
				case RegexCode.Nonboundary:
					//: if (!IsBoundary(Textpos(), _textbeg, _textend))
					//:     break Backward;
					CallIsBoundary(_regexRunnerDef.runtextposFieldDef, _regexRunnerDef.runtextbegFieldDef, _regexRunnerDef.runtextendFieldDef);
					if (Code == RegexCode.Boundary)
						Brfalse(BackwardLabel);
					else
						Brtrue(BackwardLabel);
					break;

				case RegexCode.ECMABoundary:
				case RegexCode.NonECMABoundary:
					//: if (!IsECMABoundary(Textpos(), _textbeg, _textend))
					//:     break Backward;
					CallIsECMABoundary(_regexRunnerDef.runtextposFieldDef, _regexRunnerDef.runtextbegFieldDef, _regexRunnerDef.runtextendFieldDef);
					if (Code == RegexCode.ECMABoundary)
						Brfalse(BackwardLabel);
					else
						Brtrue(BackwardLabel);
					break;

				case RegexCode.Beginning:
					//: if (Leftchars() > 0)
					//:    break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					Bgt(BackwardLabel);
					break;

				case RegexCode.Start:
					//: if (Textpos() != Textstart())
					//:    break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextstartFieldDef);
					Bne(BackwardLabel);
					break;

				case RegexCode.EndZ:
					//: if (Rightchars() > 1 || Rightchars() == 1 && CharAt(Textpos()) != '\n')
					//:    break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Ldc(1);
					Sub();
					Blt(BackwardLabel);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Bge(CodePosLabel(NextCodepos(code, codePos)));
					Rightchar();
					Ldc('\n');
					Bne(BackwardLabel);
					break;

				case RegexCode.End:
					//: if (Rightchars() > 0)
					//:    break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Blt(BackwardLabel);
					break;

				case RegexCode.One:
				case RegexCode.Notone:
				case RegexCode.Set:
				case RegexCode.One | RegexCode.Rtl:
				case RegexCode.Notone | RegexCode.Rtl:
				case RegexCode.Set | RegexCode.Rtl:
				case RegexCode.One | RegexCode.Ci:
				case RegexCode.Notone | RegexCode.Ci:
				case RegexCode.Set | RegexCode.Ci:
				case RegexCode.One | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Notone | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Set | RegexCode.Ci | RegexCode.Rtl:

					//: if (Rightchars() < 1 || Rightcharnext() != (char)Operand(0))
					//:    break Backward;
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);

					if (!IsRtl) {
						LdRunnerField(_regexRunnerDef.runtextendFieldDef);
						Bge(BackwardLabel);
						Rightcharnext();
					}
					else {
						LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
						Ble(BackwardLabel);
						Leftcharnext();
					}

					if (IsCi)
						CallToLower(options);

					if (Code == RegexCode.Set) {
						CallCharInClass(code.Strings[Operand(0)]);
						Brfalse(BackwardLabel);
					}
					else {
						Ldc(Operand(0));
						if (Code == RegexCode.One)
							Bne(BackwardLabel);
						else
							Beq(BackwardLabel);
					}
					break;

				case RegexCode.Multi:
				case RegexCode.Multi | RegexCode.Ci: {
					//: string Str = _strings[Operand(0)];
					//: int i, c;
					//: if (Rightchars() < (c = Str.Length))
					//:     break Backward;
					//: for (i = 0; c > 0; i++, c--)
					//:     if (Str[i] != Rightcharnext())
					//:         break Backward;
					var str = code.Strings[Operand(0)];

					Ldc(str.Length);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Sub();
					Bgt(BackwardLabel);

					// unroll the string
					for (var i = 0; i < str.Length; i++) {
						GetChar(i);
						if (IsCi)
							CallToLower(options);

						Ldc(str[i]);
						Bne(BackwardLabel);
					}

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldc(str.Length);
					Add();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					break;
				}


				case RegexCode.Multi | RegexCode.Rtl:
				case RegexCode.Multi | RegexCode.Ci | RegexCode.Rtl: {
					//: string Str = _strings[Operand(0)];
					//: int c;
					//: if (Leftchars() < (c = Str.Length))
					//:     break Backward;
					//: while (c > 0)
					//:     if (Str[--c] != Leftcharnext())
					//:         break Backward;
					var str = code.Strings[Operand(0)];

					Ldc(str.Length);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					Sub();
					Bgt(BackwardLabel);

					// unroll the string
					for (var i = str.Length - 1; i >= 0; i--) {
						GetChar(-(str.Length - i));
						if (IsCi)
							CallToLower(options);

						Ldc(str[i]);
						Bne(BackwardLabel);
					}

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldc(str.Length);
					Sub();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);

					break;
				}

				case RegexCode.Ref:
				case RegexCode.Ref | RegexCode.Rtl:
				case RegexCode.Ref | RegexCode.Ci:
				case RegexCode.Ref | RegexCode.Ci | RegexCode.Rtl: {
					//: int capnum = Operand(0);
					//: int j, c;
					//: if (!_match.IsMatched(capnum)) {
					//:     if (!RegexOptions.ECMAScript)
					//:         break Backward;
					//: } else {
					//:     if (Rightchars() < (c = _match.MatchLength(capnum)))
					//:         break Backward;
					//:     for (j = _match.MatchIndex(capnum); c > 0; j++, c--)
					//:         if (CharAt(j) != Rightcharnext())
					//:             break Backward;
					//: }
					var lenV = RequireLocalInt32();
					var indexV = RequireLocalInt32();
					var l1 = CreateLabel();

					CallIsMatched(Operand(0));
					if ((options & RegexOptions.ECMAScript) != 0)
						Brfalse(AdvanceLabel(code, codePos));
					else
						Brfalse(BackwardLabel); // !IsMatched() -> back

					CallMatchLength(Operand(0));
					Dup();
					Stloc(lenV);
					if (!IsRtl) {
						LdRunnerField(_regexRunnerDef.runtextendFieldDef);
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					}
					else {
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
						LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					}
					Sub();
					Bgt(BackwardLabel);         // Matchlength() > Rightchars() -> back

					CallMatchIndex(Operand(0));
					if (!IsRtl) {
						Ldloc(lenV);
						Add(false);
					}
					Stloc(indexV);              // index += len

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldloc(lenV);
					Add(IsRtl);
					StRunnerField(_regexRunnerDef.runtextposFieldDef);           // texpos += len

					MarkLabel(l1);
					Ldloc(lenV);
					Ldc(0);
					Ble(AdvanceLabel(code, codePos));
					LdRunnerField(_regexRunnerDef.runtextFieldDef);
					Ldloc(indexV);
					Ldloc(lenV);
					if (IsRtl) {
						Ldc(1);
						Sub();
						Dup();
						Stloc(lenV);
					}
					Sub(IsRtl);
					CallStringGetChars();
					if (IsCi)
						CallToLower(options);

					LdRunnerField(_regexRunnerDef.runtextFieldDef);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldloc(lenV);
					if (!IsRtl) {
						Dup();
						Ldc(1);
						Sub();
						Stloc(lenV);
					}
					Sub(IsRtl);
					CallStringGetChars();
					if (IsCi)
						CallToLower(options);

					Beq(l1);
					Back();

					FreeLocal(lenV);
					FreeLocal(indexV);
					break;
				}

				case RegexCode.Onerep:
				case RegexCode.Notonerep:
				case RegexCode.Setrep:
				case RegexCode.Onerep | RegexCode.Rtl:
				case RegexCode.Notonerep | RegexCode.Rtl:
				case RegexCode.Setrep | RegexCode.Rtl:
				case RegexCode.Onerep | RegexCode.Ci:
				case RegexCode.Notonerep | RegexCode.Ci:
				case RegexCode.Setrep | RegexCode.Ci:
				case RegexCode.Onerep | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Notonerep | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Setrep | RegexCode.Ci | RegexCode.Rtl: {
					//: int c = Operand(1);
					//: if (Rightchars() < c)
					//:     break Backward;
					//: char ch = (char)Operand(0);
					//: while (c-- > 0)
					//:     if (Rightcharnext() != ch)
					//:         break Backward;
					var lenV = RequireLocalInt32();
					var l1 = CreateLabel();

					int c = Operand(1);

					if (c == 0)
						break;

					Ldc(c);
					if (!IsRtl) {
						LdRunnerField(_regexRunnerDef.runtextendFieldDef);
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					}
					else {
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
						LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					}
					Sub();
					Bgt(BackwardLabel);         // Matchlength() > Rightchars() -> back

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldc(c);
					Add(IsRtl);
					StRunnerField(_regexRunnerDef.runtextposFieldDef);           // texpos += len

					Ldc(c);
					Stloc(lenV);

					MarkLabel(l1);
					LdRunnerField(_regexRunnerDef.runtextFieldDef);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldloc(lenV);
					if (IsRtl) {
						Ldc(1);
						Sub();
						Dup();
						Stloc(lenV);
						Add();
					}
					else {
						Dup();
						Ldc(1);
						Sub();
						Stloc(lenV);
						Sub();
					}
					CallStringGetChars();
					if (IsCi)
						CallToLower(options);

					if (Code == RegexCode.Setrep) {
						CallCharInClass(code.Strings[Operand(0)]);
						Brfalse(BackwardLabel);
					}
					else {
						Ldc(Operand(0));
						if (Code == RegexCode.Onerep)
							Bne(BackwardLabel);
						else
							Beq(BackwardLabel);
					}
					Ldloc(lenV);
					Ldc(0);
					if (Code == RegexCode.Setrep)
						Bgt(l1);
					else
						Bgt(l1);

					FreeLocal(lenV);
					break;
				}

				case RegexCode.Oneloop:
				case RegexCode.Notoneloop:
				case RegexCode.Setloop:
				case RegexCode.Oneloop | RegexCode.Rtl:
				case RegexCode.Notoneloop | RegexCode.Rtl:
				case RegexCode.Setloop | RegexCode.Rtl:
				case RegexCode.Oneloop | RegexCode.Ci:
				case RegexCode.Notoneloop | RegexCode.Ci:
				case RegexCode.Setloop | RegexCode.Ci:
				case RegexCode.Oneloop | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Notoneloop | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Setloop | RegexCode.Ci | RegexCode.Rtl: {
					//: int c = Operand(1);
					//: if (c > Rightchars())
					//:     c = Rightchars();
					//: char ch = (char)Operand(0);
					//: int i;
					//: for (i = c; i > 0; i--)
					//: {
					//:     if (Rightcharnext() != ch)
					//:     {
					//:         Leftnext();
					//:         break;
					//:     }
					//: }
					//: if (c > i)
					//:     Track(c - i - 1, Textpos() - 1);
					var cV = RequireLocalInt32();
					var lenV = RequireLocalInt32();
					var l1 = CreateLabel();
					var l2 = CreateLabel();

					int c = Operand(1);

					if (c == 0)
						break;
					if (!IsRtl) {
						LdRunnerField(_regexRunnerDef.runtextendFieldDef);
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					}
					else {
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
						LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					}
					Sub();
					if (c != Int32.MaxValue) {
						var l4 = CreateLabel();
						Dup();
						Ldc(c);
						Blt(l4);
						Pop();
						Ldc(c);
						MarkLabel(l4);
					}
					Dup();
					Stloc(lenV);
					Ldc(1);
					Add();
					Stloc(cV);

					MarkLabel(l1);
					Ldloc(cV);
					Ldc(1);
					Sub();
					Dup();
					Stloc(cV);
					Ldc(0);
					if (Code == RegexCode.Setloop)
						Ble(l2);
					else
						Ble(l2);

					if (IsRtl)
						Leftcharnext();
					else
						Rightcharnext();
					if (IsCi)
						CallToLower(options);

					if (Code == RegexCode.Setloop) {
						CallCharInClass(code.Strings[Operand(0)]);

						Brtrue(l1);
					}
					else {
						Ldc(Operand(0));
						if (Code == RegexCode.Oneloop)
							Beq(l1);
						else
							Bne(l1);
					}

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldc(1);
					Sub(IsRtl);
					StRunnerField(_regexRunnerDef.runtextposFieldDef);

					MarkLabel(l2);
					Ldloc(lenV);
					Ldloc(cV);
					Ble(AdvanceLabel(code, codePos));

					ReadyPushTrack();
					Ldloc(lenV);
					Ldloc(cV);
					Sub();
					Ldc(1);
					Sub();
					DoPush();

					ReadyPushTrack();
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldc(1);
					Sub(IsRtl);
					DoPush();

					Track(codePos);

					FreeLocal(cV);
					FreeLocal(lenV);
					break;
				}

				case RegexCode.Oneloop | RegexCode.Back:
				case RegexCode.Notoneloop | RegexCode.Back:
				case RegexCode.Setloop | RegexCode.Back:
				case RegexCode.Oneloop | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Notoneloop | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Setloop | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Oneloop | RegexCode.Ci | RegexCode.Back:
				case RegexCode.Notoneloop | RegexCode.Ci | RegexCode.Back:
				case RegexCode.Setloop | RegexCode.Ci | RegexCode.Back:
				case RegexCode.Oneloop | RegexCode.Ci | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Notoneloop | RegexCode.Ci | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Setloop | RegexCode.Ci | RegexCode.Rtl | RegexCode.Back: {
					//: Trackframe(2);
					//: int i   = Tracked(0);
					//: int pos = Tracked(1);
					//: Textto(pos);
					//: if (i > 0)
					//:     Track(i - 1, pos - 1);
					//: Advance(2);
					var tempV = RequireLocalInt32();

					PopTrack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					PopTrack();
					Stloc(tempV);
					Ldloc(tempV);
					Ldc(0);
					Ble(AdvanceLabel(code, codePos));
					ReadyPushTrack();
					Ldloc(tempV);
					Ldc(1);
					Sub();
					DoPush();
					ReadyPushTrack();
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Ldc(1);
					Sub(IsRtl);
					DoPush();
					Trackagain(backtrackCodePos);
					Advance(code, codePos);

					FreeLocal(tempV);
					break;
				}

				case RegexCode.Onelazy:
				case RegexCode.Notonelazy:
				case RegexCode.Setlazy:
				case RegexCode.Onelazy | RegexCode.Rtl:
				case RegexCode.Notonelazy | RegexCode.Rtl:
				case RegexCode.Setlazy | RegexCode.Rtl:
				case RegexCode.Onelazy | RegexCode.Ci:
				case RegexCode.Notonelazy | RegexCode.Ci:
				case RegexCode.Setlazy | RegexCode.Ci:
				case RegexCode.Onelazy | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Notonelazy | RegexCode.Ci | RegexCode.Rtl:
				case RegexCode.Setlazy | RegexCode.Ci | RegexCode.Rtl: {
					//: int c = Operand(1);
					//: if (c > Rightchars())
					//:     c = Rightchars();
					//: if (c > 0)
					//:     Track(c - 1, Textpos());
					var cV = RequireLocalInt32();

					int c = Operand(1);

					if (c == 0)
						break;

					if (!IsRtl) {
						LdRunnerField(_regexRunnerDef.runtextendFieldDef);
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					}
					else {
						LdRunnerField(_regexRunnerDef.runtextposFieldDef);
						LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					}
					Sub();
					if (c != Int32.MaxValue) {
						var l4 = CreateLabel();
						Dup();
						Ldc(c);
						Blt(l4);
						Pop();
						Ldc(c);
						MarkLabel(l4);
					}
					Dup();
					Stloc(cV);
					Ldc(0);
					Ble(AdvanceLabel(code, codePos));
					ReadyPushTrack();
					Ldloc(cV);
					Ldc(1);
					Sub();
					DoPush();
					PushTrack(_regexRunnerDef.runtextposFieldDef);
					Track(codePos);

					FreeLocal(cV);
					break;
				}

				case RegexCode.Onelazy | RegexCode.Back:
				case RegexCode.Notonelazy | RegexCode.Back:
				case RegexCode.Setlazy | RegexCode.Back:
				case RegexCode.Onelazy | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Notonelazy | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Setlazy | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Onelazy | RegexCode.Ci | RegexCode.Back:
				case RegexCode.Notonelazy | RegexCode.Ci | RegexCode.Back:
				case RegexCode.Setlazy | RegexCode.Ci | RegexCode.Back:
				case RegexCode.Onelazy | RegexCode.Ci | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Notonelazy | RegexCode.Ci | RegexCode.Rtl | RegexCode.Back:
				case RegexCode.Setlazy | RegexCode.Ci | RegexCode.Rtl | RegexCode.Back: {
					//: Trackframe(2);
					//: int pos = Tracked(1);
					//: Textto(pos);
					//: if (Rightcharnext() != (char)Operand(0))
					//:     break Backward;
					//: int i = Tracked(0);
					//: if (i > 0)
					//:     Track(i - 1, pos + 1);
					var tempV = RequireLocalInt32();

					PopTrack();
					StRunnerField(_regexRunnerDef.runtextposFieldDef);
					PopTrack();
					Stloc(tempV);

					if (!IsRtl)
						Rightcharnext();
					else
						Leftcharnext();

					if (IsCi)
						CallToLower(options);

					if (Code == RegexCode.Setlazy) {
						CallCharInClass(code.Strings[Operand(0)]);
						Brfalse(BackwardLabel);
					}
					else {
						Ldc(Operand(0));
						if (Code == RegexCode.Onelazy)
							Bne(BackwardLabel);
						else
							Beq(BackwardLabel);
					}

					Ldloc(tempV);
					Ldc(0);
					Ble(AdvanceLabel(code, codePos));
					ReadyPushTrack();
					Ldloc(tempV);
					Ldc(1);
					Sub();
					DoPush();
					PushTrack(_regexRunnerDef.runtextposFieldDef);
					Trackagain(backtrackCodePos);
					Advance(code, codePos);

					FreeLocal(tempV);
					break;
				}

				default:
					throw new NotImplementedException();
			}
		}
		private void CrawlAndUncapture() {
			// Creates something like this:
			//: while (Crawlpos() != x)
			//:     Uncapture();
			//: break Backward;
			// x in this case is what ever is on the stack. The value is kept on the
			// via duplication.

			var l1 = CreateLabel();
			var l2 = CreateLabel();

			Dup();
			CallCrawlpos();
			Beq(l2);

			MarkLabel(l1);
			CallUncapture();
			Dup();
			CallCrawlpos();
			Bne(l1);

			MarkLabel(l2);
			Pop();
			Back();
		}
	}
}
