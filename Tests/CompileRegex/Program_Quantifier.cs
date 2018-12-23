using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source of the quantifier tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/quantifiers-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void QuantifierTests() {
			QuantifierZeroOrMoreTimesTest();
			QuantifierOneOrMoreTimesTest();
			QuantifierZeroOrOneTimeTest();
			QuantifierNTimesTest();
			QuantifierAtLeastNTimesTest();
			QuantifierNtoMTimesTest();
			QuantifierZeroOrMoreTimesLazyTest();
			QuantifierOneOrMoreTimesLazyTest();
			QuantifierZeroOrOneTimeLazyTest();
			QuantifierNTimesLazyTest();
			QuantifierNtoMTimesLazyTest();
			QuantifierEmptyMatchesTest();
		}

		private static void QuantifierZeroOrMoreTimesTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierZeroOrMoreTimesTest));

			const string pattern = @"\b91*9*\b";
			string input = "99 95 919 929 9119 9219 999 9919 91119";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierOneOrMoreTimesTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierOneOrMoreTimesTest));

			const string pattern = @"\ban+\w*?\b";

			string input = "Autumn is a great time for an annual announcement to all antique collectors.";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierZeroOrOneTimeTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierZeroOrOneTimeTest));

			const string pattern = @"\ban?\b";
			string input = "An amiable animal with a large snount and an animated nose.";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierNTimesTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierNTimesTest));

			const string pattern = @"\b\d+\,\d{3}\b";
			string input = "Sales totaled 103,524 million in January, " +
								  "106,971 million in February, but only " +
								  "943 million in March.";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierAtLeastNTimesTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierAtLeastNTimesTest));

			const string pattern = @"\b\d{2,}\b\D+";
			string input = "7 days, 10 weeks, 300 years";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierNtoMTimesTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierNtoMTimesTest));

			const string pattern = @"(00\s){2,4}";
			string input = "0x00 FF 00 00 18 17 FF 00 00 00 21 00 00 00 00 00";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierZeroOrMoreTimesLazyTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierZeroOrMoreTimesLazyTest));

			const string pattern = @"\b\w*?oo\w*?\b";
			string input = "woof root root rob oof woo woe";
			foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierOneOrMoreTimesLazyTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierOneOrMoreTimesLazyTest));

			const string pattern = @"\b\w+?\b";
			string input = "Aa Bb Cc Dd Ee Ff";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierZeroOrOneTimeLazyTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierZeroOrOneTimeLazyTest));

			const string pattern = @"^\s*(System.)??Console.Write(Line)??\(??";
			string input =
				"System.Console.WriteLine(\"Hello!\")\n" +
				"Console.Write(\"Hello!\")\n" +
				"Console.WriteLine(\"Hello!\")\n" +
				"Console.ReadLine()\n" +
				"   Console.WriteLine";
			foreach (Match match in Regex.Matches(input, pattern,
												  RegexOptions.IgnorePatternWhitespace |
												  RegexOptions.IgnoreCase |
												  RegexOptions.Multiline))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierNTimesLazyTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierNTimesLazyTest));

			const string pattern = @"\b(\w{3,}?\.){2}?\w{3,}?\b";
			string input = "www.microsoft.com msdn.microsoft.com mywebsite mycompany.com";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierNtoMTimesLazyTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierNtoMTimesLazyTest));

			const string pattern = @"\b[A-Z](\w*?\s*?){1,10}[.!?]";
			string input = 
				"Hi. I am writing a short note. Its purpose is " +
				"to test a regular expression that attempts to find " +
				"sentences with ten or fewer words. Most sentences " +
				"in this note are short.";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("'{0}' found at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void QuantifierEmptyMatchesTest() {
			Console.WriteLine("START TEST: " + nameof(QuantifierEmptyMatchesTest));

			var input = "aaabbb";
			{
				const string pattern = @"(a\1|(?(1)\1)){0,2}";

				Console.WriteLine("Regex pattern: {0}", pattern);
				var match = Regex.Match(input, pattern);
				Console.WriteLine("Match: '{0}' at position {1}.", match.Value, match.Index);
				if (match.Groups.Count > 1) {
					for (int groupCtr = 1; groupCtr <= match.Groups.Count - 1; groupCtr++) {
						var group = match.Groups[groupCtr];
						Console.WriteLine("   Group: {0}: '{1}' at position {2}.", groupCtr, group.Value, group.Index);
						int captureCtr = 0;
						foreach (Capture capture in group.Captures) {
							captureCtr++;
							Console.WriteLine("      Capture: {0}: '{1}' at position {2}.", captureCtr, capture.Value, capture.Index);
						}
					}
				}
				Console.WriteLine();
			}

			{
				const string pattern = @"(a\1|(?(1)\1)){2}";
				Console.WriteLine("Regex pattern: {0}", pattern);
				var match = Regex.Match(input, pattern);
				Console.WriteLine("Matched '{0}' at position {1}.", match.Value, match.Index);
				if (match.Groups.Count > 1) {
					for (int groupCtr = 1; groupCtr <= match.Groups.Count - 1; groupCtr++) {
						var group = match.Groups[groupCtr];
						Console.WriteLine("   Group: {0}: '{1}' at position {2}.", groupCtr, group.Value, group.Index);
						int captureCtr = 0;
						foreach (Capture capture in group.Captures) {
							captureCtr++;
							Console.WriteLine("      Capture: {0}: '{1}' at position {2}.", captureCtr, capture.Value, capture.Index);
						}
					}
				}
			}
		}
	}
}
