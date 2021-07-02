using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Confuser.Renamer {
	public class MessageDeobfuscator {
		static readonly Regex MapSymbolMatcher = new Regex("_[a-zA-Z0-9]+");
		static readonly Regex PasswordSymbolMatcher = new Regex("[a-zA-Z0-9_$]{23,}");

		readonly Dictionary<string, string> _symbolMap;
		readonly ReversibleRenamer _renamer;

		public static MessageDeobfuscator Load(string symbolMapFileName) {
			if (symbolMapFileName is null)
				throw new ArgumentNullException(nameof(symbolMapFileName));

			var symbolMap = new Dictionary<string, string>();
			using (var reader = new StreamReader(File.OpenRead(symbolMapFileName))) {
				var line = reader.ReadLine();
				while (line != null) {
					int tabIndex = line.IndexOf('\t');
					if (tabIndex == -1)
						throw new FileFormatException();
					symbolMap.Add(line.Substring(0, tabIndex), line.Substring(tabIndex + 1));
					line = reader.ReadLine();
				}
			}

			return new MessageDeobfuscator(symbolMap);
		}

		public MessageDeobfuscator(Dictionary<string, string> map) => _symbolMap = map ?? throw new ArgumentNullException(nameof(map));

		public MessageDeobfuscator(string password) => _renamer = new ReversibleRenamer(password);

		public string Deobfuscate(string obfuscatedMessage) {
			if (_symbolMap != null) {
				return MapSymbolMatcher.Replace(obfuscatedMessage, DecodeSymbolMap);
			}

			return PasswordSymbolMatcher.Replace(obfuscatedMessage, DecodeSymbolPassword);
		}

		string DecodeSymbolMap(Match match) {
			var symbol = match.Value;
			if (_symbolMap.TryGetValue(symbol, out string result))
				return ExtractShortName(result);
			return ExtractShortName(symbol);
		}

		string DecodeSymbolPassword(Match match) {
			var sym = match.Value;
			try {
				return ExtractShortName(_renamer.Decrypt(sym));
			}
			catch {
				return sym;
			}
		}

		static string ExtractShortName(string fullName) {
			const string doubleParen = "::";
			int doubleParenIndex = fullName.IndexOf(doubleParen, StringComparison.Ordinal);
			if (doubleParenIndex != -1) {
				int resultStringStartIndex = doubleParenIndex + doubleParen.Length;
				int parenIndex = fullName.IndexOf('(', doubleParenIndex);
				return fullName.Substring(resultStringStartIndex,
					(parenIndex == -1 ? fullName.Length : parenIndex) - resultStringStartIndex);
			}

			int slashIndex = fullName.IndexOf('/');
			if (slashIndex != -1) {
				return fullName.Substring(slashIndex + 1);
			}

			return fullName;
		}
	}
}
