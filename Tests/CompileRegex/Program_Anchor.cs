using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source for the anchor tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/anchors-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void AnchorTests() {
			AnchorStartOfStringOrLineTest();
			AnchorEndOfStringOrLineTest();
			AnchorStartOfStringOnlyTest();
			AnchorEndOfStringOrBeforeEndingNewLineTest();
			AnchorEndOfStringOnlyTest();
			AnchorContiguousMatchesTest();
			AnchorWordBoundaryTest();
			AnchorNonWordBoundaryTest();
		}

		private static void AnchorStartOfStringOrLineTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorStartOfStringOrLineTest));

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

		private static void AnchorEndOfStringOrLineTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorEndOfStringOrLineTest));

			int startPos = 0, endPos = 70;
			string cr = Environment.NewLine;
			string input = "Brooklyn Dodgers, National League, 1911, 1912, 1932-1957" + cr +
						   "Chicago Cubs, National League, 1903-present" + cr +
						   "Detroit Tigers, American League, 1901-present" + cr +
						   "New York Giants, National League, 1885-1957" + cr +
						   "Washington Senators, American League, 1901-1960" + cr;
			Match match;

			const string basePattern = @"^((\w+(\s?)){2,}),\s(\w+\s\w+),(\s\d{4}(-(\d{4}|present))?,?)+";
			const string pattern = basePattern + "$";
			Console.WriteLine("Attempting to match the entire input string:");
			if (input.Substring(startPos, endPos).Contains(",")) {
				match = Regex.Match(input, pattern);
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

			string[] teams = input.Split(new String[] { cr }, StringSplitOptions.RemoveEmptyEntries);
			Console.WriteLine("Attempting to match each element in a string array:");
			foreach (string team in teams) {
				if (team.Length > 70) continue;

				match = Regex.Match(team, pattern);
				if (match.Success) {
					Console.Write("The {0} played in the {1} in",
								  match.Groups[1].Value, match.Groups[4].Value);
					foreach (Capture capture in match.Groups[5].Captures)
						Console.Write(capture.Value);
					Console.WriteLine(".");
				}
			}
			Console.WriteLine();

			startPos = 0;
			endPos = 70;
			Console.WriteLine("Attempting to match each line of an input string with '$':");
			if (input.Substring(startPos, endPos).Contains(",")) {
				match = Regex.Match(input, pattern, RegexOptions.Multiline);
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

			startPos = 0;
			endPos = 70;
			const string pattern2 = basePattern + "\r?$";
			Console.WriteLine(@"Attempting to match each line of an input string with '\r?$':");
			if (input.Substring(startPos, endPos).Contains(",")) {
				match = Regex.Match(input, pattern2, RegexOptions.Multiline);
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
			Console.WriteLine();
		}

		private static void AnchorStartOfStringOnlyTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorStartOfStringOnlyTest));

			int startPos = 0, endPos = 70;
			string input = "Brooklyn Dodgers, National League, 1911, 1912, 1932-1957\n" +
						   "Chicago Cubs, National League, 1903-present\n" +
						   "Detroit Tigers, American League, 1901-present\n" +
						   "New York Giants, National League, 1885-1957\n" +
						   "Washington Senators, American League, 1901-1960\n";

			const string pattern = @"\A((\w+(\s?)){2,}),\s(\w+\s\w+),(\s\d{4}(-(\d{4}|present))?,?)+";
			Match match;

			if (input.Substring(startPos, endPos).Contains(",")) {
				match = Regex.Match(input, pattern, RegexOptions.Multiline);
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
			Console.WriteLine();
		}

		private static void AnchorEndOfStringOrBeforeEndingNewLineTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorEndOfStringOrBeforeEndingNewLineTest));

			string[] inputs = { "Brooklyn Dodgers, National League, 1911, 1912, 1932-1957",
						  "Chicago Cubs, National League, 1903-present" + Environment.NewLine,
						  "Detroit Tigers, American League, 1901-present" + Regex.Unescape(@"\n"),
						  "New York Giants, National League, 1885-1957",
						  "Washington Senators, American League, 1901-1960" + Environment.NewLine};
			const string pattern = @"^((\w+(\s?)){2,}),\s(\w+\s\w+),(\s\d{4}(-(\d{4}|present))?,?)+\r?\Z";

			foreach (string input in inputs) {
				if (input.Length > 70 || !input.Contains(",")) continue;

				Console.WriteLine(Regex.Escape(input));
				var match = Regex.Match(input, pattern);
				if (match.Success)
					Console.WriteLine("   Match succeeded.");
				else
					Console.WriteLine("   Match failed.");
			}
			Console.WriteLine();
		}

		private static void AnchorEndOfStringOnlyTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorEndOfStringOnlyTest));

			string[] inputs = { "Brooklyn Dodgers, National League, 1911, 1912, 1932-1957",
						  "Chicago Cubs, National League, 1903-present" + Environment.NewLine,
						  "Detroit Tigers, American League, 1901-present\\r",
						  "New York Giants, National League, 1885-1957",
						  "Washington Senators, American League, 1901-1960" + Environment.NewLine };
			const string pattern = @"^((\w+(\s?)){2,}),\s(\w+\s\w+),(\s\d{4}(-(\d{4}|present))?,?)+\r?\z";

			foreach (string input in inputs) {
				if (input.Length > 70 || !input.Contains(",")) continue;

				Console.WriteLine(Regex.Escape(input));
				var match = Regex.Match(input, pattern);
				if (match.Success)
					Console.WriteLine("   Match succeeded.");
				else
					Console.WriteLine("   Match failed.");
			}
			Console.WriteLine();
		}

		private static void AnchorContiguousMatchesTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorContiguousMatchesTest));

			string input = "capybara,squirrel,chipmunk,porcupine,gopher," +
						   "beaver,groundhog,hamster,guinea pig,gerbil," +
						   "chinchilla,prairie dog,mouse,rat";
			const string pattern = @"\G(\w+\s?\w*),?";
			var match = Regex.Match(input, pattern);
			while (match.Success) {
				Console.WriteLine(match.Groups[1].Value);
				match = match.NextMatch();
			}
			Console.WriteLine();
		}

		private static void AnchorWordBoundaryTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorWordBoundaryTest));

			string input = "area bare arena mare";
			const string pattern = @"\bare\w*\b";
			Console.WriteLine("Words that begin with 'are':");
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void AnchorNonWordBoundaryTest() {
			Console.WriteLine("START TEST: " + nameof(AnchorNonWordBoundaryTest));

			string input = "equity queen equip acquaint quiet";
			const string pattern = @"\Bqu\w+";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}", match.Value, match.Index);

			Console.WriteLine();
		}
	}
}
