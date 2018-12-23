using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source for the grouping tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/grouping-constructs-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void GroupingTests() {
			GroupingMatchedSubexpressionTest();
			GroupingNamedMatchedSubexpressionTest();
			GroupingBalancingGroupDefinitionTest();
			GroupingNonCapturingGroupTest();
			GroupingGroupOptionsTest();
			GroupingZeroWidthPositiveLookAheadAssertTest();
			GroupingZeroWidthNegativeLookAheadAssertTest();
			GroupingZeroWidthPositiveLookBehindAssertTest();
			GroupingZeroWidthNegativeLookBehindAssertTest();
			GroupingNonBacktracingSubexpressionTest();
			GroupingConstructTest();
		}

		private static void GroupingMatchedSubexpressionTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingMatchedSubexpressionTest));

			const string pattern = @"(\w+)\s(\1)";
			string input = "He said that that was the the correct answer.";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("Duplicate '{0}' found at positions {1} and {2}.",
								  match.Groups[1].Value, match.Groups[1].Index, match.Groups[2].Index);

			Console.WriteLine();
		}

		private static void GroupingNamedMatchedSubexpressionTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingNamedMatchedSubexpressionTest));

			const string pattern = @"(?<duplicateWord>\w+)\s\k<duplicateWord>\W(?<nextWord>\w+)";
			string input = "He said that that was the the correct answer.";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("A duplicate '{0}' at position {1} is followed by '{2}'.",
								  match.Groups["duplicateWord"].Value, match.Groups["duplicateWord"].Index,
								  match.Groups["nextWord"].Value);

			Console.WriteLine();
		}

		private static void GroupingBalancingGroupDefinitionTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingBalancingGroupDefinitionTest));

			const string pattern = "^[^<>]*" +
								   "(" +
								   "((?'Open'<)[^<>]*)+" +
								   "((?'Close-Open'>)[^<>]*)+" +
								   ")*" +
								   "(?(Open)(?!))$";
			string input = "<abc><mno<xyz>>";

			var m = Regex.Match(input, pattern);
			if (m.Success == true) {
				Console.WriteLine("Input: \"{0}\"", input);
				Console.WriteLine("Match: \"{0}\"", m);
				int grpCtr = 0;
				foreach (Group grp in m.Groups) {
					Console.WriteLine("   Group {0}: {1}", grpCtr, grp.Value);
					grpCtr++;
					int capCtr = 0;
					foreach (Capture cap in grp.Captures) {
						Console.WriteLine("      Capture {0}: {1}", capCtr, cap.Value);
						capCtr++;
					}
				}
			}
			else {
				Console.WriteLine("Match failed.");
			}
			Console.WriteLine();
		}

		private static void GroupingNonCapturingGroupTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingNonCapturingGroupTest));

			const string pattern = @"(?:\b(?:\w+)\W*)+\.";
			string input = "This is a short sentence.";
			var match = Regex.Match(input, pattern);
			Console.WriteLine("Match: {0}", match.Value);
			for (int ctr = 1; ctr < match.Groups.Count; ctr++)
				Console.WriteLine("   Group {0}: {1}", ctr, match.Groups[ctr].Value);

			Console.WriteLine();
		}

		private static void GroupingGroupOptionsTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingGroupOptionsTest));

			const string pattern = @"\b(?ix: d \w+)\s";
			string input = "Dogs are decidedly good pets.";

			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}// found at index {1}.", match.Value, match.Index);

			Console.WriteLine();
		}

		private static void GroupingZeroWidthPositiveLookAheadAssertTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingZeroWidthPositiveLookAheadAssertTest));

			const string pattern = @"\b\w+(?=\sis\b)";
			string[] inputs = { "The dog is a Malamute.",
						  "The island has beautiful birds.",
						  "The pitch missed home plate.",
						  "Sunday is a weekend day." };

			foreach (string input in inputs) {
				var match = Regex.Match(input, pattern);
				if (match.Success)
					Console.WriteLine("'{0}' precedes 'is'.", match.Value);
				else
					Console.WriteLine("'{0}' does not match the pattern.", input);
			}
			Console.WriteLine();
		}

		private static void GroupingZeroWidthNegativeLookAheadAssertTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingZeroWidthNegativeLookAheadAssertTest));

			const string pattern = @"\b(?!un)\w+\b";
			string input = "unite one unethical ethics use untie ultimate";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine(match.Value);
			Console.WriteLine();
		}

		private static void GroupingZeroWidthPositiveLookBehindAssertTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingZeroWidthPositiveLookBehindAssertTest));

			string input = "2010 1999 1861 2140 2009";
			const string pattern = @"(?<=\b20)\d{2}\b";

			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(match.Value);
			Console.WriteLine();
		}

		private static void GroupingZeroWidthNegativeLookBehindAssertTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingZeroWidthNegativeLookBehindAssertTest));

			string[] dates = {
				"Monday February 1, 2010",
				"Wednesday February 3, 2010",
				"Saturday February 6, 2010",
				"Sunday February 7, 2010",
				"Monday, February 8, 2010"
			};
			const string pattern = @"(?<!(Saturday|Sunday) )\b\w+ \d{1,2}, \d{4}\b";

			foreach (string dateValue in dates) {
				var match = Regex.Match(dateValue, pattern);
				if (match.Success)
					Console.WriteLine(match.Value);
			}
			Console.WriteLine();
		}

		private static void GroupingNonBacktracingSubexpressionTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingNonBacktracingSubexpressionTest));

			string[] inputs = { "cccd.", "aaad", "aaaa" };
			const string back = @"(\w)\1+.\b";
			const string noback = @"(?>(\w)\1+).\b";

			foreach (string input in inputs) {
				var match1 = Regex.Match(input, back);
				var match2 = Regex.Match(input, noback);
				Console.WriteLine("{0}:", input);

				Console.Write("   Backtracking: ");
				if (match1.Success)
					Console.WriteLine(match1.Value);
				else
					Console.WriteLine("No match");

				Console.Write("   Nonbacktracking: ");
				if (match2.Success)
					Console.WriteLine(match2.Value);
				else
					Console.WriteLine("No match");
			}
			Console.WriteLine();
		}

		private static void GroupingConstructTest() {
			Console.WriteLine("START TEST: " + nameof(GroupingConstructTest));

			const string pattern = @"(\b(\w+)\W+)+";
			string input = "This is a short sentence.";
			var match = Regex.Match(input, pattern);
			Console.WriteLine("Match: '{0}'", match.Value);
			for (int ctr = 1; ctr < match.Groups.Count; ctr++) {
				Console.WriteLine("   Group {0}: '{1}'", ctr, match.Groups[ctr].Value);
				int capCtr = 0;
				foreach (Capture capture in match.Groups[ctr].Captures) {
					Console.WriteLine("      Capture {0}: '{1}'", capCtr, capture.Value);
					capCtr++;
				}
			}
			Console.WriteLine();
		}
	}
}
