using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source for the options tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/regular-expression-options?view=netframework-4.7.2
		/// </summary>
		private static void OptionsTests() {
			OptionsCaseInsensitiveTest();
			OptionsCaseInsensitiveInlineTest();
			OptionsMultilineTest();
			OptionsMultilineInlineTest();
			OptionsSinglelineTest();
			OptionsSinglelineInlineTest();
			OptionsExplicitCaptureTest();
			OptionsExplicitCaptureInlineTest();
			OptionsExplicitCaptureInlineByGroupTest();
			OptionsIgnoreWhitespaceTest();
			OptionsIgnoreWhitespaceInlineTest();
			OptionsRightToLeftTest();
			OptionsRightToLeftLookBehindTest();
			OptionsEcmaScriptTest();
			OptionsEcmaScriptSelfRefGroupTest();
			OptionsCultureInvariantTest();
		}

		private static void OptionsCaseInsensitiveTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsCaseInsensitiveTest));

			const string pattern = @"\bthe\w*\b";
			string input = "The man then told them about that event.";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("Found {0} at index {1}.", match.Value, match.Index);

			Console.WriteLine();
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("Found {0} at index {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void OptionsCaseInsensitiveInlineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsCaseInsensitiveInlineTest));

			string input = "The man then told them about that event.";
			{
				const string pattern = @"\b(?i:t)he\w*\b";
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine("Found {0} at index {1}.", match.Value, match.Index);
				Console.WriteLine();
			}

			{
				const string pattern = @"(?i)\bthe\w*\b";
				foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
					Console.WriteLine("Found {0} at index {1}.", match.Value, match.Index);
				Console.WriteLine();
			}
		}

		private static void OptionsMultilineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsMultilineTest));

			var scores = new SortedList<int, string>(new DescendingComparer<int>());

			string input =
				"Joe 164\n" +
				"Sam 208\n" +
				"Allison 211\n" +
				"Gwen 171\n";
			{
				const string pattern = @"^(\w+)\s(\d+)$";
				bool matched = false;

				Console.WriteLine("Without Multiline option:");
				foreach (Match match in Regex.Matches(input, pattern)) {
					scores.Add(Int32.Parse(match.Groups[2].Value), (string)match.Groups[1].Value);
					matched = true;
				}
				if (!matched)
					Console.WriteLine("   No matches.");
				Console.WriteLine();
			}


			{
				const string pattern = @"^(\w+)\s(\d+)\r*$";
				Console.WriteLine("With multiline option:");
				foreach (Match match in Regex.Matches(input, pattern, RegexOptions.Multiline))
					scores.Add(Int32.Parse(match.Groups[2].Value), (string)match.Groups[1].Value);

				// List scores in descending order. 
				foreach (var score in scores)
					Console.WriteLine("{0}: {1}", score.Value, score.Key);
				Console.WriteLine();
			}
		}

		private static void OptionsMultilineInlineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsMultilineInlineTest));

			var scores = new SortedList<int, string>(new DescendingComparer<int>());

			string input =
				"Joe 164\n" +
				"Sam 208\n" +
				"Allison 211\n" +
				"Gwen 171\n";
			const string pattern = @"(?m)^(\w+)\s(\d+)\r*$";

			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.Multiline))
				scores.Add(Convert.ToInt32(match.Groups[2].Value), match.Groups[1].Value);

			// List scores in descending order. 
			foreach (var score in scores)
				Console.WriteLine("{0}: {1}", score.Value, score.Key);
			Console.WriteLine();
		}

		private static void OptionsSinglelineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsSinglelineTest));

			const string pattern = "^.+";
			string input = "This is one line and" + Environment.NewLine + "this is the second.";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(Regex.Escape(match.Value));

			Console.WriteLine();
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.Singleline))
				Console.WriteLine(Regex.Escape(match.Value));
			Console.WriteLine();
		}

		private static void OptionsSinglelineInlineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsSinglelineInlineTest));

			const string pattern = "(?s)^.+";
			string input = "This is one line and" + Environment.NewLine + "this is the second.";

			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(Regex.Escape(match.Value));
			Console.WriteLine();
		}

		private static void OptionsExplicitCaptureTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsExplicitCaptureTest));

			string input =
				"This is the first sentence. Is it the beginning " +
				"of a literary masterpiece? I think not. Instead, " +
				"it is a nonsensical paragraph.";
			const string pattern = @"\b\(?((?>\w+),?\s?)+[\.!?]\)?";
			Console.WriteLine("With implicit captures:");
			foreach (Match match in Regex.Matches(input, pattern)) {
				Console.WriteLine("The match: {0}", match.Value);
				int groupCtr = 0;
				foreach (Group group in match.Groups) {
					Console.WriteLine("   Group {0}: {1}", groupCtr, group.Value);
					groupCtr++;
					int captureCtr = 0;
					foreach (Capture capture in group.Captures) {
						Console.WriteLine("      Capture {0}: {1}", captureCtr, capture.Value);
						captureCtr++;
					}
				}
			}
			Console.WriteLine();
			Console.WriteLine("With explicit captures only:");
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.ExplicitCapture)) {
				Console.WriteLine("The match: {0}", match.Value);
				int groupCtr = 0;
				foreach (Group group in match.Groups) {
					Console.WriteLine("   Group {0}: {1}", groupCtr, group.Value);
					groupCtr++;
					int captureCtr = 0;
					foreach (Capture capture in group.Captures) {
						Console.WriteLine("      Capture {0}: {1}", captureCtr, capture.Value);
						captureCtr++;
					}
				}
			}
			Console.WriteLine();
		}

		private static void OptionsExplicitCaptureInlineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsExplicitCaptureInlineTest));

			string input =
				"This is the first sentence. Is it the beginning " +
				"of a literary masterpiece? I think not. Instead, " +
				"it is a nonsensical paragraph.";
			const string pattern = @"(?n)\b\(?((?>\w+),?\s?)+[\.!?]\)?";

			foreach (Match match in Regex.Matches(input, pattern)) {
				Console.WriteLine("The match: {0}", match.Value);
				int groupCtr = 0;
				foreach (Group group in match.Groups) {
					Console.WriteLine("   Group {0}: {1}", groupCtr, group.Value);
					groupCtr++;
					int captureCtr = 0;
					foreach (Capture capture in group.Captures) {
						Console.WriteLine("      Capture {0}: {1}", captureCtr, capture.Value);
						captureCtr++;
					}
				}
			}
			Console.WriteLine();
		}

		private static void OptionsExplicitCaptureInlineByGroupTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsExplicitCaptureInlineByGroupTest));

			string input =
				"This is the first sentence. Is it the beginning " +
				"of a literary masterpiece? I think not. Instead, " +
				"it is a nonsensical paragraph.";
			const string pattern = @"\b\(?(?n:(?>\w+),?\s?)+[\.!?]\)?";

			foreach (Match match in Regex.Matches(input, pattern)) {
				Console.WriteLine("The match: {0}", match.Value);
				int groupCtr = 0;
				foreach (Group group in match.Groups) {
					Console.WriteLine("   Group {0}: {1}", groupCtr, group.Value);
					groupCtr++;
					int captureCtr = 0;
					foreach (Capture capture in group.Captures) {
						Console.WriteLine("      Capture {0}: {1}", captureCtr, capture.Value);
						captureCtr++;
					}
				}
			}
			Console.WriteLine();
		}

		private static void OptionsIgnoreWhitespaceTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsIgnoreWhitespaceTest));

			string input =
				"This is the first sentence. Is it the beginning " +
				"of a literary masterpiece? I think not. Instead, " +
				"it is a nonsensical paragraph.";
			const string pattern = @"\b\(?((?>\w+),?\s?)+[\.!?]\)?";

			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnorePatternWhitespace))
				Console.WriteLine(match.Value);
			Console.WriteLine();
		}

		private static void OptionsIgnoreWhitespaceInlineTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsIgnoreWhitespaceInlineTest));

			string input =
				"This is the first sentence. Is it the beginning " +
				"of a literary masterpiece? I think not. Instead, " +
				"it is a nonsensical paragraph.";
			const string pattern = @"(?x)\b \(? ( (?>\w+) ,?\s? )+  [\.!?] \)? # Matches an entire sentence.";

			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(match.Value);
			Console.WriteLine();
		}

		private static void OptionsRightToLeftTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsRightToLeftTest));

			const string pattern = @"\bb\w+\s";
			string input = "builder rob rabble";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.RightToLeft))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void OptionsRightToLeftLookBehindTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsRightToLeftLookBehindTest));

			string[] inputs = { "1 May 1917", "June 16, 2003" };
			const string pattern = @"(?<=\d{1,2}\s)\w+,?\s\d{4}";

			foreach (string input in inputs) {
				var match = Regex.Match(input, pattern, RegexOptions.RightToLeft);
				if (match.Success)
					Console.WriteLine("The date occurs in {0}.", match.Value);
				else
					Console.WriteLine("{0} does not match.", input);
			}
			Console.WriteLine();
		}

		private static void OptionsEcmaScriptTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsEcmaScriptTest));

			string[] values = { "äöü", "aou" };
			const string pattern = @"\b(\w+\s*)+";
			foreach (var value in values) {
				Console.Write("Canonical matching: ");
				if (Regex.IsMatch(value, pattern))
					Console.WriteLine("'{0}' matches the pattern.", value);
				else
					Console.WriteLine("'{0}' does not match the pattern.", value);

				Console.Write("ECMAScript matching: ");
				if (Regex.IsMatch(value, pattern, RegexOptions.ECMAScript))
					Console.WriteLine("'{0}' matches the pattern.", value);
				else
					Console.WriteLine("'{0}' does not match the pattern.", value);
				Console.WriteLine();
			}
		}

		private static void OptionsEcmaScriptSelfRefGroupTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsEcmaScriptSelfRefGroupTest));

			const string pattern = @"((a+)(\1) ?)+";

			void AnalyzeMatch(Match m) {
				if (m.Success) {
					Console.WriteLine("'{0}' matches {1} at position {2}.", pattern, m.Value, m.Index);
					int grpCtr = 0;
					foreach (Group grp in m.Groups) {
						Console.WriteLine("   {0}: '{1}'", grpCtr, grp.Value);
						grpCtr++;
						int capCtr = 0;
						foreach (Capture cap in grp.Captures) {
							Console.WriteLine("      {0}: '{1}'", capCtr, cap.Value);
							capCtr++;
						}
					}
				}
				else {
					Console.WriteLine("No match found.");
				}
				Console.WriteLine();
			}

			string input = "aa aaaa aaaaaa ";

			// Match input using canonical matching.
			AnalyzeMatch(Regex.Match(input, pattern));

			// Match input using ECMAScript.
			AnalyzeMatch(Regex.Match(input, pattern, RegexOptions.ECMAScript));
		}

		private static void OptionsCultureInvariantTest() {
			Console.WriteLine("START TEST: " + nameof(OptionsCultureInvariantTest));

			string input = "file://c:/Documents.MyReport.doc";
			const string pattern = "FILE://";

			Console.WriteLine("Culture-insensitive matching...");
			if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
				Console.WriteLine("URLs that access files are not allowed.");
			else
				Console.WriteLine("Access to {0} is allowed.", input);

			Console.WriteLine();
		}

		private sealed class DescendingComparer<T> : IComparer<T> {
			public int Compare(T x, T y) => Comparer<T>.Default.Compare(x, y) * -1;
		}
	}
}
