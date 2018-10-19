using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AntiProtections {
	public class Program {

		internal static int Main(string[] args) {
			Console.OutputEncoding = Encoding.UTF8;

			Console.WriteLine("START");
			EcmaMatchingTest();
			AnchorMatchingTest();
			MatchedSubexpressionTest();
			NamedMatchedSubexpressionTest();
			BalancingGroupDefinitionTest();
			NonCapturingGroupTest();
			GroupOptionsTest();
			Console.WriteLine("END");
			return 42;
		}

		private static void EcmaMatchingTest() {
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

		private static void AnchorMatchingTest() {
			int startPos = 0, endPos = 70;
			string input = "Brooklyn Dodgers, National League, 1911, 1912, 1932-1957\n" +
						   "Chicago Cubs, National League, 1903-present\n" +
						   "Detroit Tigers, American League, 1901-present\n" +
						   "New York Giants, National League, 1885-1957\n" +
						   "Washington Senators, American League, 1901-1960\n";
			const string pattern = @"^((\w+(\s?)){2,}),\s(\w+\s\w+),(\s\d{4}(-(\d{4}|present))?,?)+";

			if (input.Substring(startPos, endPos).Contains(",")) {
				var match = Regex.Match(input, pattern);
				while (match.Success) {
					Console.Write("The {0} played in the {1} in",
									  match.Groups[1].Value, match.Groups[4].Value);
					foreach (Capture capture in match.Groups[5].Captures)
						Console.Write(capture.Value);

					Console.WriteLine(".");
					startPos = match.Index + match.Length;
					endPos = startPos + 70 <= input.Length ? 70 : input.Length - startPos;
					if (!input.Substring(startPos, endPos).Contains(",")) break;
					match = match.NextMatch();
				}
				Console.WriteLine();
			}

			if (input.Substring(startPos, endPos).Contains(",")) {
				var match = Regex.Match(input, pattern, RegexOptions.Multiline);
				while (match.Success) {
					Console.Write("The {0} played in the {1} in",
									  match.Groups[1].Value, match.Groups[4].Value);
					foreach (Capture capture in match.Groups[5].Captures)
						Console.Write(capture.Value);

					Console.WriteLine(".");
					startPos = match.Index + match.Length;
					endPos = startPos + 70 <= input.Length ? 70 : input.Length - startPos;
					if (!input.Substring(startPos, endPos).Contains(",")) break;
					match = match.NextMatch();
				}
				Console.WriteLine();
			}
		}

		private static void MatchedSubexpressionTest() {
			const string pattern = @"(\w+)\s(\1)";
			string input = "He said that that was the the correct answer.";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("Duplicate '{0}' found at positions {1} and {2}.",
								  match.Groups[1].Value, match.Groups[1].Index, match.Groups[2].Index);

			Console.WriteLine();
		}

		private static void NamedMatchedSubexpressionTest() {
			const string pattern = @"(?<duplicateWord>\w+)\s\k<duplicateWord>\W(?<nextWord>\w+)";
			string input = "He said that that was the the correct answer.";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("A duplicate '{0}' at position {1} is followed by '{2}'.",
								  match.Groups["duplicateWord"].Value, match.Groups["duplicateWord"].Index,
								  match.Groups["nextWord"].Value);

			Console.WriteLine();
		}

		private static void BalancingGroupDefinitionTest() {
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

		private static void NonCapturingGroupTest() {
			const string pattern = @"(?:\b(?:\w+)\W*)+\.";
			string input = "This is a short sentence.";
			var match = Regex.Match(input, pattern);
			Console.WriteLine("Match: {0}", match.Value);
			for (int ctr = 1; ctr < match.Groups.Count; ctr++)
				Console.WriteLine("   Group {0}: {1}", ctr, match.Groups[ctr].Value);

			Console.WriteLine();
		}

		private static void GroupOptionsTest() {
			const string pattern = @"\b(?ix: d \w+)\s";
			string input = "Dogs are decidedly good pets.";

			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}// found at index {1}.", match.Value, match.Index);

			Console.WriteLine();
		}
	}
}
