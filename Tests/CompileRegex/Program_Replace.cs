using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source for the replacement tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/substitutions-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void ReplacementTests() {
			ReplacingNumberedGroupTest();
			ReplacingNamedGroupTest();
			ReplacingEntireMatchTest();
			ReplacingBeforeMatchTest();
			ReplaceAfterMatchTest();
			ReplaceLastCaptureGroupTest();
			ReplaceEntireStringTest();
		}

		private static void ReplacingNumberedGroupTest() {
			Console.WriteLine("START TEST: " + nameof(ReplacingNumberedGroupTest));

			const string pattern = @"\p{Sc}*(\s?\d+[.,]?\d*)\p{Sc}*";
			string replacement = "$1";
			string input = "$16.32 12.19 £16.29 €18.29  €18,29";
			string result = Regex.Replace(input, pattern, replacement);
			Console.WriteLine(result);
			Console.WriteLine();
		}

		private static void ReplacingNamedGroupTest() {
			Console.WriteLine("START TEST: " + nameof(ReplacingNamedGroupTest));

			const string pattern = @"\p{Sc}*(?<amount>\s?\d+[.,]?\d*)\p{Sc}*";
			string replacement = "${amount}";
			string input = "$16.32 12.19 £16.29 €18.29  €18,29";
			string result = Regex.Replace(input, pattern, replacement);
			Console.WriteLine(result);
			Console.WriteLine();
		}

		private static void ReplacingEntireMatchTest() {
			Console.WriteLine("START TEST: " + nameof(ReplacingEntireMatchTest));

			const string pattern = @"^(\w+\s?)+$";
			string[] titles = { "A Tale of Two Cities",
						  "The Hound of the Baskervilles",
						  "The Protestant Ethic and the Spirit of Capitalism",
						  "The Origin of Species" };
			string replacement = "\"$&\"";
			foreach (string title in titles)
				Console.WriteLine(Regex.Replace(title, pattern, replacement));
			Console.WriteLine();
		}

		private static void ReplacingBeforeMatchTest() {
			Console.WriteLine("START TEST: " + nameof(ReplacingBeforeMatchTest));

			string input = "aa1bb2cc3dd4ee5";
			const string pattern = @"\d+";
			string substitution = "$`";
			Console.WriteLine("Matches:");
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("   {0} at position {1}", match.Value, match.Index);

			Console.WriteLine("Input string:  {0}", input);
			Console.WriteLine("Output string: " + Regex.Replace(input, pattern, substitution));
			Console.WriteLine();
		}

		private static void ReplaceAfterMatchTest() {
			Console.WriteLine("START TEST: " + nameof(ReplaceAfterMatchTest));

			string input = "aa1bb2cc3dd4ee5";
			const string pattern = @"\d+";
			string substitution = "$'";
			Console.WriteLine("Matches:");
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("   {0} at position {1}", match.Value, match.Index);
			Console.WriteLine("Input string:  {0}", input);
			Console.WriteLine("Output string: " + Regex.Replace(input, pattern, substitution));
			Console.WriteLine();
		}

		private static void ReplaceLastCaptureGroupTest() {
			Console.WriteLine("START TEST: " + nameof(ReplaceLastCaptureGroupTest));

			const string pattern = @"\b(\w+)\s\1\b";
			string substitution = "$+";
			string input = "The the dog jumped over the fence fence.";
			Console.WriteLine(Regex.Replace(input, pattern, substitution, RegexOptions.IgnoreCase));
			Console.WriteLine();
		}

		private static void ReplaceEntireStringTest() {
			Console.WriteLine("START TEST: " + nameof(ReplaceEntireStringTest));

			string input = "ABC123DEF456";
			const string pattern = @"\d+";
			string substitution = "$_";
			Console.WriteLine("Original string:          {0}", input);
			Console.WriteLine("String with substitution: {0}", Regex.Replace(input, pattern, substitution));
			Console.WriteLine();
		}
	}
}
