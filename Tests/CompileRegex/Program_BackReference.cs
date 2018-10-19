using System;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {
		/// <summary>
		/// The source for the back reference tests is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/backreference-constructs-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void BackReferenceTests() {
			BackReferenceNumberedTest();
			BackReferenceNamedTest();
			BackReferenceNamedNumericTest();
			BackReferenceRecentTest();
		}

		private static void BackReferenceNumberedTest() {
			Console.WriteLine("START TEST: " + nameof(BackReferenceNumberedTest));

			const string pattern = @"(\w)\1";
			string input = "trellis llama webbing dresser swagger";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("Found '{0}' at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void BackReferenceNamedTest() {
			Console.WriteLine("START TEST: " + nameof(BackReferenceNamedTest));

			const string pattern = @"(?<char>\w)\k<char>";
			string input = "trellis llama webbing dresser swagger";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("Found '{0}' at position {1}.", match.Value, match.Index);
			Console.WriteLine();
		}

		private static void BackReferenceNamedNumericTest() {
			Console.WriteLine("START TEST: " + nameof(BackReferenceNamedNumericTest));

			const string pattern = @"(?<2>\w)\k<2>";
			string input = "trellis llama webbing dresser swagger";
			foreach (Match match in Regex.Matches(input, pattern))
				Console.WriteLine("Found '{0}' at position {1}.", match.Value, match.Index);
			Console.WriteLine();

			Console.WriteLine(Regex.IsMatch("aa", @"(?<char>\w)\k<1>"));
			Console.WriteLine();
		}

		private static void BackReferenceRecentTest() {
			Console.WriteLine("START TEST: " + nameof(BackReferenceRecentTest));

			{
				const string pattern = @"(?<1>a)(?<1>\1b)*";
				string input = "aababb";
				foreach (Match match in Regex.Matches(input, pattern)) {
					Console.WriteLine("Match: " + match.Value);
					foreach (Group group in match.Groups)
						Console.WriteLine("   Group: " + group.Value);
				}
				Console.WriteLine();
			}

			{
				const string pattern = @"\b(\p{Lu}{2})(\d{2})?(\p{Lu}{2})\b";
				string[] inputs = { "AA22ZZ", "AABB" };
				foreach (string input in inputs) {
					var match = Regex.Match(input, pattern);
					if (match.Success) {
						Console.WriteLine("Match in {0}: {1}", input, match.Value);
						if (match.Groups.Count > 1) {
							for (int ctr = 1; ctr <= match.Groups.Count - 1; ctr++) {
								if (match.Groups[ctr].Success)
									Console.WriteLine("Group {0}: {1}", ctr, match.Groups[ctr].Value);
								else
									Console.WriteLine("Group {0}: <no match>", ctr);
							}
						}
					}
					Console.WriteLine();
				}
			}
		}
	}
}
