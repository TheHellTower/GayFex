using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Optimizations.CompileRegex.Compiler {
	internal sealed class RegexRunnerFindFirstCharMethodCompiler : RegexRunnerMethodCompiler {
		internal RegexRunnerFindFirstCharMethodCompiler(ModuleDef module, MethodDef method, RegexRunnerDef regexRunnerDef) :
			base(module, method, regexRunnerDef) {
		}

		internal void GenerateFindFirstChar(RegexOptions options, RegexCode code) {
			if (IsAnchorFindFirstChar(code))
				GenerateAnchorFindFirstChar(code);
			else if (IsBoyerMooreFindFirstChar(code))
				GenerateBoyerMooreFindFirstChar(options, code);
			else if (IsPrefixFindFirstChar(code))
				GeneratePrefixFindFirstChar(options, code);
			else
				GenerateExitSuccess();

			FinishMethod();
		}

		private static bool IsAnchorFindFirstChar(RegexCode code) =>
			(code._anchors & (RegexFCD.Beginning | RegexFCD.Start | RegexFCD.EndZ | RegexFCD.End)) != 0;

		private static bool IsBoyerMooreFindFirstChar(RegexCode code) =>
			code._bmPrefix != null && code._bmPrefix._negativeUnicode == null;

		private static bool IsPrefixFindFirstChar(RegexCode code) => code._fcPrefix != null;

		private void GenerateAnchorFindFirstChar(RegexCode code) {
			var anchors = code._anchors;
			var rightToLeft = code._rightToLeft;
			var bmPrefix = code._bmPrefix;

			var endFoundNothing = CreateLabel();

			if (!rightToLeft) {
				if ((anchors & RegexFCD.Beginning) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					Ble(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextendFieldDef));
					Br(endFoundNothing);
					MarkLabel(l1);
				}

				if ((anchors & RegexFCD.Start) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextstartFieldDef);
					Ble(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextendFieldDef));
					Br(endFoundNothing);
					MarkLabel(l1);
				}

				if ((anchors & RegexFCD.EndZ) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Ldc(1);
					Sub();
					Bge(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => {
						LdRunnerField(_regexRunnerDef.runtextendFieldDef);
						Ldc(1);
						Sub();
					});
					MarkLabel(l1);
				}

				if ((anchors & RegexFCD.End) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Bge(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextendFieldDef));
					MarkLabel(l1);
				}
			}
			else {
				if ((anchors & RegexFCD.End) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Bge(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextbegFieldDef));
					Br(endFoundNothing);
					MarkLabel(l1);
				}

				if ((anchors & RegexFCD.EndZ) != 0) {
					var l1 = CreateLabel();
					var l2 = CreateLabel();
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Ldc(1);
					Sub();
					Blt(l1);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
					Beq(l2);
					LdRunnerField(_regexRunnerDef.runtextFieldDef);
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					CallStringGetChars();
					Ldc((int)'\n');
					Beq(l2);
					MarkLabel(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextbegFieldDef));
					Br(endFoundNothing);
					MarkLabel(l2);
				}

				if ((anchors & RegexFCD.Start) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextstartFieldDef);
					Bge(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextbegFieldDef));
					Br(endFoundNothing);
					MarkLabel(l1);
				}

				if ((anchors & RegexFCD.Beginning) != 0) {
					var l1 = CreateLabel();

					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
					Ble(l1);
					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => LdRunnerField(_regexRunnerDef.runtextbegFieldDef));
					MarkLabel(l1);
				}
			}

			// <
			GenerateExitSuccess();

			MarkLabel(endFoundNothing);
			GenerateExitFail();
		}

		private void GenerateBoyerMooreFindFirstChar(RegexOptions options, RegexCode code) {
			var rightToLeft = code._rightToLeft;
			var bmPrefix = code._bmPrefix;

			// Compiled Boyer-Moore string matching
			// <
			var lFail = CreateLabel();
			var lPartialMatch = CreateLabel();

			IList<RegexMethodCompilerLabel> table;

			int beforefirst;
			int last;
			if (!rightToLeft) {
				beforefirst = -1;
				last = bmPrefix._pattern.Length - 1;
			}
			else {
				beforefirst = bmPrefix._pattern.Length;
				last = 0;
			}

			var chLast = bmPrefix._pattern[last];

			if (!rightToLeft)
				LdRunnerField(_regexRunnerDef.runtextendFieldDef);
			else
				LdRunnerField(_regexRunnerDef.runtextbegFieldDef);

			{
				var lDefaultAdvance = CreateLabel();
				var lAdvance = CreateLabel();
				var lStart = CreateLabel();

				var limitV = RequireLocalInt32();
				Stloc(limitV);

				StRunnerField(_regexRunnerDef.runtextposFieldDef, () => {
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					if (!rightToLeft) {
						Ldc(bmPrefix._pattern.Length - 1);
						Add();
					}
					else {
						Ldc(bmPrefix._pattern.Length);
						Sub();
					}
				});
				Br(lStart);

				MarkLabel(lDefaultAdvance);

				if (!rightToLeft)
					Ldc(bmPrefix._pattern.Length);
				else
					Ldc(-bmPrefix._pattern.Length);

				MarkLabel(lAdvance);

				StRunnerField(_regexRunnerDef.runtextposFieldDef, () => {
					LdRunnerField(_regexRunnerDef.runtextposFieldDef);
					Add();
				});

				MarkLabel(lStart);

				LdRunnerField(_regexRunnerDef.runtextposFieldDef);
				Ldloc(limitV);

				if (!rightToLeft)
					Bge(lFail);
				else
					Blt(lFail);

				Rightchar();
				if (bmPrefix._caseInsensitive)
					CallToLower(options);

				Dup();
				{
					var chV = RequireLocalInt32();

					Stloc(chV);
					Ldc(chLast);
					Beq(lPartialMatch);

					Ldloc(chV);

					FreeLocal(chV);
				}
				Ldc(bmPrefix._lowASCII);
				Sub();
				Dup();
				{
					var chV = RequireLocalInt32();

					Stloc(chV);
					Ldc(bmPrefix._highASCII - bmPrefix._lowASCII);
					Bgtun(lDefaultAdvance);

					table = new RegexMethodCompilerLabel[bmPrefix._highASCII - bmPrefix._lowASCII + 1];

					for (var i = bmPrefix._lowASCII; i <= bmPrefix._highASCII; i++) {
						if (bmPrefix._negativeASCII[i] == beforefirst)
							table[i - bmPrefix._lowASCII] = lDefaultAdvance;
						else
							table[i - bmPrefix._lowASCII] = CreateLabel();
					}

					Ldloc(chV);

					FreeLocal(chV);
				}
				Add(Instruction.Create(OpCodes.Switch, table.Select(GetLabelInstruction).ToArray()));

				for (var i = bmPrefix._lowASCII; i <= bmPrefix._highASCII; i++) {
					if (bmPrefix._negativeASCII[i] == beforefirst)
						continue;

					MarkLabel(table[i - bmPrefix._lowASCII]);

					Ldc(bmPrefix._negativeASCII[i]);
					Br(lAdvance);
				}

				MarkLabel(lPartialMatch);

				LdRunnerField(_regexRunnerDef.runtextposFieldDef);
				{
					var testV = RequireLocalInt32();

					Stloc(testV);

					for (var i = bmPrefix._pattern.Length - 2; i >= 0; i--) {
						var lNext = CreateLabel();
						int charindex;

						if (!rightToLeft)
							charindex = i;
						else
							charindex = bmPrefix._pattern.Length - 1 - i;

						LdRunnerField(_regexRunnerDef.runtextFieldDef);
						Ldloc(testV);
						Ldc(1);
						Sub(rightToLeft);
						Dup();
						Stloc(testV);
						CallStringGetChars();
						if (bmPrefix._caseInsensitive)
							CallToLower(options);

						Ldc(bmPrefix._pattern[charindex]);
						Beq(lNext);
						Ldc(bmPrefix._positive[charindex]);
						Br(lAdvance);

						MarkLabel(lNext);
					}

					StRunnerField(_regexRunnerDef.runtextposFieldDef, () => {
						Ldloc(testV);
						if (rightToLeft) {
							Ldc(1);
							Add();
						}
					});

					FreeLocal(testV);
				}

				FreeLocal(limitV);
			}

			GenerateExitSuccess();

			MarkLabel(lFail);

			StRunnerField(_regexRunnerDef.runtextposFieldDef, () => {
				if (!rightToLeft)
					LdRunnerField(_regexRunnerDef.runtextendFieldDef);
				else
					LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
			});
			GenerateExitFail();
		}

		private void GeneratePrefixFindFirstChar(RegexOptions options, RegexCode code) {
			var rightToLeft = code._rightToLeft;
			var fcPrefix = code._fcPrefix;

			var endFoundNothing = CreateLabel();
			var endFoundFirstChar = CreateLabel();

			var l1 = CreateLabel();
			var l2 = CreateLabel();
			var l3 = CreateLabel();

			if (!rightToLeft) {
				LdRunnerField(_regexRunnerDef.runtextendFieldDef);
				LdRunnerField(_regexRunnerDef.runtextposFieldDef);
			}
			else {
				LdRunnerField(_regexRunnerDef.runtextposFieldDef);
				LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
			}
			Sub();

			{
				var cV = RequireLocalInt32();

				Stloc(cV);

				Ldloc(cV);
				Ldc(0);
				Ble(endFoundNothing);

				MarkLabel(l1);

				Ldloc(cV);

				FreeLocal(cV);
			}

			Ldc(1);
			Sub();

			{
				var cV = RequireLocalInt32();

				Stloc(cV);

				if (rightToLeft)
					Leftcharnext();
				else
					Rightcharnext();

				if (fcPrefix.CaseInsensitive)
					CallToLower(options);

				if (!RegexCharClass.IsSingleton(fcPrefix.Prefix)) {
					CallCharInClass(fcPrefix.Prefix);
					Brtrue(l2);
				}
				else {
					Ldc(RegexCharClass.SingletonChar(fcPrefix.Prefix));
					Beq(l2);
				}

				MarkLabel(l3);

				Ldloc(cV);

				FreeLocal(cV);
			}
			Ldc(0);
			Bgt(l1);

			Br(endFoundFirstChar);

			MarkLabel(l2);

			/*          // CURRENTLY DISABLED
						// If for some reason we have a prefix we didn't use, use it now.

						if (_bmPrefix != null) {
							if (!rightToLeft) {
								LdRunnerField(_regexRunnerDef.runtextendFieldDef);
								LdRunnerField(_regexRunnerDef.runtextposFieldDef);
							}
							else {
								LdRunnerField(_regexRunnerDef.runtextposFieldDef);
								LdRunnerField(_regexRunnerDef.runtextbegFieldDef);
							}
							Sub();
							Ldc(_bmPrefix._pattern.Length - 1);
							BltFar(l5);

							for (int i = 1; i < _bmPrefix._pattern.Length; i++) {
								Ldloc(_textV);
								LdRunnerField(_regexRunnerDef.runtextposFieldDef);
								if (!rightToLeft) {
									Ldc(i - 1);
									Add();
								}
								else {
									Ldc(i);
									Sub();
								}
								Callvirt(_getcharM);
								if (!rightToLeft)
									Ldc(_bmPrefix._pattern[i]);
								else
									Ldc(_bmPrefix._pattern[_bmPrefix._pattern.Length - 1 - i]);
								BneFar(l5);
							}
						}
			*/

			StRunnerField(_regexRunnerDef.runtextposFieldDef, () => {
				LdRunnerField(_regexRunnerDef.runtextposFieldDef);
				Ldc(1);
				Sub(rightToLeft);
			});

			MarkLabel(endFoundFirstChar);
			GenerateExitSuccess();

			MarkLabel(endFoundNothing);
			GenerateExitFail();
		}

		private void GenerateExitSuccess() => GenerateExit(true);

		private void GenerateExitFail() => GenerateExit(false);

		private void GenerateExit(bool retValue) {
			UpdateCachedField(_regexRunnerDef.runtextposFieldDef);
			Ldc(retValue ? 1 : 0);
			Ret();
		}
	}
}
