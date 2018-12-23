using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source for the alternating tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/alternation-constructs-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void AlternatingTests() {
			AlternatingWithVerticalBarTest();
			AlternatingConditionalTest();
			AlternatingConditionalCaptureGroupTest();
		}

		private static void AlternatingWithVerticalBarTest() {
			Console.WriteLine("START TEST: " + nameof(AlternatingWithVerticalBarTest));

			{
				string input = "The gray wolf blended in among the grey rocks.";
				const string pattern = @"\bgr(a|e)y\b";
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine("'{0}' found at position {1}", match.Value, match.Index);
				Console.WriteLine();
			}

			{
				const string pattern = @"\b(\d{2}-\d{7}|\d{3}-\d{2}-\d{4})\b";
				string input = "01-9999999 020-333333 777-88-9999";
				Console.WriteLine("Matches for {0}:", pattern);
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine("   {0} at position {1}", match.Value, match.Index);
				Console.WriteLine();
			}
		}

		private static void AlternatingConditionalTest() {
			Console.WriteLine("START TEST: " + nameof(AlternatingConditionalTest));

			const string pattern = @"\b(?(\d{2}-)\d{2}-\d{7}|\d{3}-\d{2}-\d{4})\b";
			string input = "01-9999999 020-333333 777-88-9999";
			Console.WriteLine("Matches for {0}:", pattern);
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("   {0} at position {1}", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void AlternatingConditionalCaptureGroupTest() {
			Console.WriteLine("START TEST: " + nameof(AlternatingConditionalCaptureGroupTest));

			{
				const string pattern = @"\b(?<n2>\d{2}-)?(?(n2)\d{7}|\d{3}-\d{2}-\d{4})\b";
				string input = "01-9999999 020-333333 777-88-9999";
				Console.WriteLine("Matches for {0}:", pattern);
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine("   {0} at position {1}", match.Value, match.Index);
				Console.WriteLine();
			}

			{
				const string pattern = @"\b(\d{2}-)?(?(1)\d{7}|\d{3}-\d{2}-\d{4})\b";
				string input = "01-9999999 020-333333 777-88-9999";
				Console.WriteLine("Matches for {0}:", pattern);
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine("   {0} at position {1}", match.Value, match.Index);
			}
		}
	}
}
