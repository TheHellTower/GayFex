using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CompileRegex {
	public static partial class Program {

		internal static int Main(string[] args) {
			Console.OutputEncoding = Encoding.UTF8;

			Console.WriteLine("START");

			MicrosoftDocsTests();

			Console.WriteLine("END");
			return 42;
		}

		/// <summary>
		/// All these tests are extracted from the examples provided here:
		/// https://docs.microsoft.com/dotnet/standard/base-types/regular-expression-language-quick-reference?view=netframework-4.7.2
		/// </summary>
		private static void MicrosoftDocsTests() {
			EscapeTest();
			CharacterClassTests();
			AnchorTests();
			GroupingTests();
			QuantifierTests();
			BackReferenceTests();
			AlternatingTests();
			ReplacementTests();
			OptionsTests();
		}

		/// <summary>
		/// The source of the escape character test is:
		/// https://docs.microsoft.com/dotnet/standard/base-types/character-escapes-in-regular-expressions?view=netframework-4.7.2
		/// </summary>
		private static void EscapeTest() {
			Console.WriteLine("START TEST: " + nameof(EscapeTest));

			const string delimited = @"\G(.+)[\t\u007c](.+)\r?\n";
			string input =
				"Mumbai, India|13,922,125\t\n" +
				"Shanghai, China\t13,831,900\n" +
				"Karachi, Pakistan|12,991,000\n" +
				"Delhi, India\t12,259,230\n" +
				"Istanbul, Turkey|11,372,613\n";
			Console.WriteLine("Population of the World's Largest Cities, 2009");
			Console.WriteLine();
			Console.WriteLine("{0,-20} {1,10}", "City", "Population");
			Console.WriteLine();
			foreach (Match match in Regex.Matches(input, delimited))
				Console.WriteLine("{0,-20} {1,10}", match.Groups[1].Value, match.Groups[2].Value);
			Console.WriteLine();
		}
	}
}
