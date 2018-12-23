using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// Source of the character class tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/character-classes-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void CharacterClassTests() {
			CharacterClassPositiveCharGroupTest();
			CharacterClassNegativeCharGroupTest();
			CharacterClassAnyCharTest();
			CharacterClassUnicodeTest();
			CharacterClassNegativeUnicodeTest();
			CharacterClassWordCharTest();
			CharacterClassNonWordCharTest();
			CharacterClassWhiteSpaceCharTest();
			CharacterClassNonWhiteSpceCharTest();
			CharacterClassDecimalDigitTest();
			CharacterClassNonDigitTest();
			CharacterClassSubstractionTest();
		}

		private static void CharacterClassPositiveCharGroupTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassPositiveCharGroupTest));

			{
				const string pattern = @"gr[ae]y\s\S+?[\s\p{P}]";
				string input = "The gray wolf jumped over the grey wall.";
				var matches = Regex.Matches(input, pattern);
				foreach (Match match in matches)
					Console.WriteLine($"'{match.Value}'");
				Console.WriteLine();
			}

			{
				const string pattern = @"\b[A-Z]\w*\b";
				string input = "A city Albany Zulu maritime Marseilles";
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine(match.Value);
				Console.WriteLine();
			}
		}

		private static void CharacterClassNegativeCharGroupTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassNegativeCharGroupTest));

			const string pattern = @"\bth[^o]\w+\b";
			string input = "thought thing though them through thus thorough this";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(match.Value);
			Console.WriteLine();
		}

		private static void CharacterClassAnyCharTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassAnyCharTest));

			{
				const string pattern = "^.+";
				string input = "This is one line and" + Environment.NewLine + "this is the second.";

				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine(Regex.Escape(match.Value));
				Console.WriteLine();

				foreach (Match match in Regex.Matches(input, pattern, RegexOptions.Singleline))
					Console.WriteLine(Regex.Escape(match.Value));
				Console.WriteLine();
			}

			{
				const string pattern = @"\b.*[.?!;:](\s|\z)";
				string input = "this. what: is? go, thing.";
				foreach (Match match in Regex.Matches(input, pattern))
					Console.WriteLine(match.Value);
				Console.WriteLine();
			}
		}

		private static void CharacterClassUnicodeTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassUnicodeTest));

			const string pattern = @"\b(\p{IsGreek}+(\s)?)+\p{Pd}\s(\p{IsBasicLatin}+(\s)?)+";
			string input = "Κατα Μαθθαίον - The Gospel of Matthew";

			Console.WriteLine(Regex.IsMatch(input, pattern));
			Console.WriteLine();
		}

		private static void CharacterClassNegativeUnicodeTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassNegativeUnicodeTest));

			const string pattern = @"(\P{Sc})+";

			string[] values = { "$164,091.78", "£1,073,142.68", "73¢", "€120" };
			foreach (string value in values)
				Console.WriteLine(Regex.Match(value, pattern).Value);
			Console.WriteLine();
		}

		private static void CharacterClassWordCharTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassWordCharTest));

			const string pattern = @"(\w)\1";
			string[] words = { "trellis", "seer", "latter", "summer",
						 "hoarse", "lesser", "aardvark", "stunned" };
			foreach (string word in words) {
				var match = Regex.Match(word, pattern);
				if (match.Success)
					Console.WriteLine("'{0}' found in '{1}' at position {2}.",
									  match.Value, word, match.Index);
				else
					Console.WriteLine("No double characters in '{0}'.", word);
			}
			Console.WriteLine();
		}

		private static void CharacterClassNonWordCharTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassNonWordCharTest));

			const string pattern = @"\b(\w+)(\W){1,2}";
			string input = "The old, grey mare slowly walked across the narrow, green pasture.";
			foreach (Match match in Regex.Matches(input, pattern)) {
				Console.WriteLine(match.Value);
				Console.Write("   Non-word character(s):");
				var captures = match.Groups[2].Captures;
				for (int ctr = 0; ctr < captures.Count; ctr++)
					Console.Write(@"'{0}' (\u{1}){2}", captures[ctr].Value,
								  Convert.ToUInt16(captures[ctr].Value[0]).ToString("X4"),
								  ctr < captures.Count - 1 ? ", " : "");
				Console.WriteLine();
			}
			Console.WriteLine();
		}

		private static void CharacterClassWhiteSpaceCharTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassWhiteSpaceCharTest));

			const string pattern = @"\b\w+(e)?s(\s|$)";
			string input = "matches stores stops leave leaves";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(match.Value);
			Console.WriteLine();
		}

		private static void CharacterClassNonWhiteSpceCharTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassNonWhiteSpceCharTest));

			const string pattern = @"\b(\S+)\s?";
			string input = "This is the first sentence of the first paragraph. " +
								  "This is the second sentence.\n" +
								  "This is the only sentence of the second paragraph.";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine(match.Groups[1]);
			Console.WriteLine();
		}

		private static void CharacterClassDecimalDigitTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassDecimalDigitTest));

			const string pattern = @"^(\(?\d{3}\)?[\s-])?\d{3}-\d{4}$";
			string[] inputs = { "111 111-1111", "222-2222", "222 333-444",
						  "(212) 111-1111", "111-AB1-1111",
						  "212-111-1111", "01 999-9999" };

			foreach (string input in inputs) {
				if (Regex.IsMatch(input, pattern))
					Console.WriteLine(input + ": matched");
				else
					Console.WriteLine(input + ": match failed");
			}
			Console.WriteLine();
		}

		private static void CharacterClassNonDigitTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassNonDigitTest));

			const string pattern = @"^\D\d{1,5}\D*$";
			string[] inputs = { "A1039C", "AA0001", "C18A", "Y938518" };

			foreach (string input in inputs) {
				if (Regex.IsMatch(input, pattern))
					Console.WriteLine(input + ": matched");
				else
					Console.WriteLine(input + ": match failed");
			}
			Console.WriteLine();
		}

		private static void CharacterClassSubstractionTest() {
			Console.WriteLine("START TEST: " + nameof(CharacterClassSubstractionTest));

			string[] inputs = { "123", "13579753", "3557798", "335599901" };
			const string pattern = @"^[0-9-[2468]]+$";

			foreach (string input in inputs) {
				var match = Regex.Match(input, pattern);
				if (match.Success)
					Console.WriteLine(match.Value);
			}
			Console.WriteLine();
		}
	}
}
