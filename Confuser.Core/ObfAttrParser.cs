using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Tree;
using Microsoft.Extensions.Logging;

namespace Confuser.Core {
	using ISettingsDictionary = IDictionary<IConfuserComponent, IDictionary<string, string>>;

	internal static class ObfAttrParser {
		public static bool TryParse(IReadOnlyDictionary<string, IProtection> items, string str, ILogger logger) {
			var inputStream = new AntlrInputStream(str);
			var lexer = new ObfAttrLexer(inputStream);
			var stream = new CommonTokenStream(lexer);
			var parser = new ObfAttrProtectionParser(stream);
			parser.SetupLogger(logger);

			var visitor = new ValidateProtectionNamesVisitor(items);
			return visitor.Visit(parser.protectionString());
		}

		public static ISettingsDictionary ParseProtection(IReadOnlyDictionary<string, IProtection> items, 
			ISettingsDictionary settings, string str, ILogger logger) {
			var inputStream = new AntlrInputStream(str);
			var lexer = new ObfAttrLexer(inputStream);
			var stream = new CommonTokenStream(lexer);
			var parser = new ObfAttrProtectionParser(stream);
			parser.SetupLogger(logger);

			var expr = parser.protectionString();
			var visitor = new ObfProtectionAttrParser(items, settings);
			
			return visitor.Visit(expr);
		}

		public static (IPacker Packer, IDictionary<string, string> PackerParams) ParsePacker(
			IReadOnlyDictionary<string, IPacker> packers, string attrFeatureValue, ILogger logger) {
			var inputStream = new AntlrInputStream(attrFeatureValue);
			var lexer = new ObfAttrLexer(inputStream);
			var stream = new CommonTokenStream(lexer);
			var parser = new ObfAttrPackerParser(stream);
			parser.SetupLogger(logger);

			var expr = parser.packerString();
			var visitor = new ObfPackerAttrVisitor(packers);
			
			return visitor.Visit(expr);
		}

		private static void SetupLogger<TSymbol, TAtnInterpreter>(this Recognizer<TSymbol, TAtnInterpreter> recognizer,
			ILogger logger) where TAtnInterpreter : ATNSimulator {
#if NETFRAMEWORK
			recognizer.RemoveErrorListener(ConsoleErrorListener<TSymbol>.Instance);
#endif
			recognizer.AddErrorListener(new LoggerAntlrErrorListener<TSymbol>(logger));
		}

		private sealed class ValidateProtectionNamesVisitor : ObfAttrProtectionParserBaseVisitor<bool> {

			private IReadOnlyDictionary<string, IProtection> Items { get; }

			protected override bool DefaultResult => true;
			public ValidateProtectionNamesVisitor(IReadOnlyDictionary<string, IProtection> items) => Items = items;

			protected override bool AggregateResult(bool aggregate, bool nextResult) => aggregate && nextResult;

			public override bool VisitItemName(ObfAttrProtectionParser.ItemNameContext context) {
				var itemName = context.GetText();
				return Items.ContainsKey(itemName);
			}
		}

		private sealed class ObfProtectionAttrParser : ObfAttrProtectionParserBaseVisitor<ISettingsDictionary> {
			private IReadOnlyDictionary<string, IProtection> Items { get; }

			private ISettingsDictionary Settings { get; }

			protected override ISettingsDictionary DefaultResult => Settings;

			public ObfProtectionAttrParser(IReadOnlyDictionary<string, IProtection> items, ISettingsDictionary settings) {
				Items = items;
				Settings = settings;
			}

			public override ISettingsDictionary Visit(IParseTree tree) {
				base.Visit(tree);
				return Settings;
			}

			public override ISettingsDictionary VisitPresetValue(ObfAttrProtectionParser.PresetValueContext context) {
				if (Enum.TryParse(context.GetText(), true, out ProtectionPreset preset)) {
					foreach (var item in Items.Values.Where(prot => prot.Preset <= preset)) {
						if (item.Preset != ProtectionPreset.None && !Settings.ContainsKey(item))
							Settings.Add(item, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
					}
				}

				return Settings;
			}

			public override ISettingsDictionary VisitItem(ObfAttrProtectionParser.ItemContext context) {
				var disable = (context.itemEnable()?.MINUS() != null);
				var itemName = context.itemName().GetText();
				var itemValues = context.itemValues();

				if (Items.TryGetValue(itemName, out var protection)) {
					if (disable) {
						if (itemValues == null)
							Settings.Remove(protection);
						else if (Settings.TryGetValue(protection, out var protectionSettings)) {
							foreach (var name in itemValues.itemValue().Select(v => v.itemValueName().GetText())) {
								protectionSettings.Remove(name);
							}
						}
					}
					else {
						if (itemValues == null) {
							if (!Settings.ContainsKey((protection)))
								Settings.Add(protection, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
						}
						else {
							if (!Settings.TryGetValue(protection, out var protectionSettings))
								protectionSettings = Settings[protection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

							foreach (var itemValue in itemValues.itemValue()) {
								protectionSettings.Add(itemValue.itemValueName().GetText(), itemValue.itemValueValue().GetText().Trim('\''));
							}
						}
					}
				}

				return Settings;
			}
		}

		private sealed class ObfPackerAttrVisitor : ObfAttrPackerParserBaseVisitor<(IPacker, IDictionary<string, string>)> {
			private IReadOnlyDictionary<string, IPacker> Packers { get; }
			private IPacker Packer { get; set; }
			private IDictionary<string, string> PackerParameter { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

			internal ObfPackerAttrVisitor(IReadOnlyDictionary<string, IPacker> packers) => 
				Packers = packers ?? throw new ArgumentNullException(nameof(packers));

			protected override (IPacker, IDictionary<string, string>) DefaultResult => (Packer, PackerParameter);

			public override (IPacker, IDictionary<string, string>) VisitPacker(ObfAttrPackerParser.PackerContext context) {
				var packerName = context.itemName().GetText();
				if (Packers.TryGetValue(packerName, out var packer))
					Packer = packer;

				base.VisitPacker(context);
				return DefaultResult;
			}

			public override (IPacker, IDictionary<string, string>) VisitItemValue(ObfAttrPackerParser.ItemValueContext context) {
				PackerParameter[context.itemValueName().GetText()] = context.itemValueValue().GetText();
				return DefaultResult;
			}
		}
	}
}
