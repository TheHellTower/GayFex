using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Confuser.Renamer {
	public class MessageDeobfuscator {
		static readonly Regex MapSymbolRegex = new Regex("_[a-zA-Z0-9]+");
		static readonly Regex PasswordSymbolRegex = new Regex("[a-zA-Z0-9_$]{23,}");

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

		public string DeobfuscateMessage(string message) {
			if (_symbolMap != null) {
				return MapSymbolRegex.Replace(message, m => DeobfuscateSymbol(m.Value, true));
			}

			return PasswordSymbolRegex.Replace(message, m => DeobfuscateSymbol(m.Value, true));
		}

		public string DeobfuscateSymbol(string obfuscatedIdentifier, bool shortName) {
			string fullName;

			if (_symbolMap != null) {
				if (!_symbolMap.TryGetValue(obfuscatedIdentifier, out fullName))
					fullName = obfuscatedIdentifier;
			}
			else {
				try {
					fullName = _renamer.Decrypt(obfuscatedIdentifier);
				}
				catch {
					fullName = obfuscatedIdentifier;
				}
			}

			return shortName ? ExtractShortName(fullName) : fullName;
		}

		public static string ExtractShortName(string fullName) {
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
