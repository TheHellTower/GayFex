using System;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using dnlib.DotNet;

namespace Confuser.Core.Project {
	using ILogger = Microsoft.Extensions.Logging.ILogger;

	/// <summary>
	///     Parser of pattern expressions.
	/// </summary>
	public partial class PatternParser {
		public static IPattern Parse(string str, ILogger logger) {
			var inputStream = new AntlrInputStream(str);
			var lexer = new PatternLexer(inputStream);
			var stream = new CommonTokenStream(lexer);
			var parser = new PatternParser(stream);
			SetupLogger(parser, logger);

			return parser.pattern();
		}

		private static void SetupLogger<TSymbol, TAtnInterpreter>(Recognizer<TSymbol, TAtnInterpreter> recognizer,
			ILogger logger) where TAtnInterpreter : ATNSimulator {
#if NETFRAMEWORK
			recognizer.RemoveErrorListener(ConsoleErrorListener<TSymbol>.Instance);
#endif
			recognizer.AddErrorListener(new LoggerAntlrErrorListener<TSymbol>(logger));
		}

		public partial class PatternContext : IPattern {
			public bool Evaluate(IDnlibDef definition) {
				var visitor = new EvaluationVisitor(definition);
				return visitor.Visit(this);
			}
		}

		public partial class LiteralExpressionContext {
			public string GetCleanedText() {
				var text = GetText();
				if (text[0] == '\'')
					return text.Substring(1, text.Length - 2).Replace("\\'", "'");
				if (text[0] == '"')
					return text.Substring(1, text.Length - 2).Replace("\\\"", "\"");
				throw new InvalidPatternException("Unexpected quoting of literal expression.");
			}
		}

		private sealed class EvaluationVisitor : PatternParserBaseVisitor<bool> {
			private readonly IDnlibDef _def;

			public EvaluationVisitor(IDnlibDef def) =>
				_def = def ?? throw new ArgumentNullException(nameof(def));

			public override bool VisitPattern(PatternContext context) {
				var childPattern = context.pattern();

				switch (childPattern.Length) {
					case 0:
						return base.VisitPattern(context);

					case 1:
						if (context.NOT() != null)
							return !Visit(childPattern[0]);
						else if (context.PAREN_OPEN() != null && context.PAREN_CLOSE() != null)
							return Visit(childPattern[0]);
						else
							throw new InvalidPatternException("Unexpected nested pattern!?");

					case 2:
						if (context.AND() != null)
							return Visit(childPattern[0]) && Visit(childPattern[1]);
						else if (context.OR() != null)
							return Visit(childPattern[0]) || Visit(childPattern[1]);
						else
							throw new InvalidPatternException("Unexpected nested pattern!?");

					default:
						throw new InvalidPatternException("More than 2 child patterns?!");
				}
			}

			public override bool VisitDeclTypeFunction(DeclTypeFunctionContext context) {
				if (!(_def is IMemberDef memberDef) || memberDef.DeclaringType == null)
					return false;

				var fullName = context.literalExpression().GetCleanedText();
				return string.Equals(memberDef.DeclaringType.FullName, fullName, StringComparison.Ordinal);
			}

			public override bool VisitFalseLiteral(FalseLiteralContext context) => false;

			public override bool VisitFullNameFunction(FullNameFunctionContext context) {
				var fullName = context.literalExpression().GetCleanedText();
				return string.Equals(_def.FullName, fullName, StringComparison.Ordinal);
			}

			public override bool VisitHasAttrFunction(HasAttrFunctionContext context) {
				var attrName = context.literalExpression().GetCleanedText();
				return _def.CustomAttributes.IsDefined(attrName);
			}

			public override bool VisitInheritsFunction(InheritsFunctionContext context) {
				var name = context.literalExpression().GetCleanedText();

				var type = (_def as IMemberDef)?.DeclaringType ?? _def as TypeDef;
				return type != null && (type.InheritsFrom(name) || type.Implements(name));
			}

			public override bool VisitIsPublicFunction(IsPublicFunctionContext context) {
				if (!(_def is IMemberDef memberDef))
					return false;

				var declType = memberDef.DeclaringType;
				while (declType != null) {
					if (!declType.IsPublic) return false;
					declType = declType.DeclaringType;
				}

				if (memberDef is MethodDef methodDef)
					return methodDef.IsVisibleOutside();
				if (memberDef is FieldDef fieldDef)
					return fieldDef.IsVisibleOutside();
				if (memberDef is PropertyDef propertyDef)
					return propertyDef.IsVisibleOutside();
				if (memberDef is EventDef eventDef)
					return eventDef.IsVisibleOutside();
				if (memberDef is TypeDef typeDef)
					return typeDef.IsVisibleOutside();

				return false;
			}

			public override bool VisitIsTypeFunction(IsTypeFunctionContext context) {
				var type = (_def as IMemberDef)?.DeclaringType ?? _def as TypeDef;
				if (type == null)
					return false;

				string typeRegex = context.literalExpression().GetCleanedText();

				var typeType = new StringBuilder();

				if (type.IsEnum)
					typeType.Append("enum ");

				if (type.IsInterface)
					typeType.Append("interface ");

				if (type.IsValueType)
					typeType.Append("valuetype ");

				if (type.IsDelegate())
					typeType.Append("delegate ");

				if (type.IsAbstract)
					typeType.Append("abstract ");

				if (type.IsNested)
					typeType.Append("nested ");

				if (type.IsSerializable)
					typeType.Append("serializable ");

				return Regex.IsMatch(typeType.ToString(), typeRegex);
			}

			public override bool VisitMatchFunction(MatchFunctionContext context) {
				string regex = context.literalExpression().GetCleanedText();
				return Regex.IsMatch(_def.FullName, regex);
			}

			public override bool VisitMatchNameFunction(MatchNameFunctionContext context) {
				string regex = context.literalExpression().GetCleanedText();
				return Regex.IsMatch(_def.Name, regex);
			}

			public override bool VisitMatchTypeNameFunction(MatchTypeNameFunctionContext context) {
				string regex = context.literalExpression().GetCleanedText();
				switch (_def) {
					case TypeDef _:
						return Regex.IsMatch(_def.Name, regex);
					case IMemberDef memberDef when memberDef.DeclaringType != null:
						return Regex.IsMatch(memberDef.DeclaringType.Name, regex);
					default:
						return false;
				}
			}

			public override bool VisitMemberTypeFunction(MemberTypeFunctionContext context) {
				string typeRegex = context.literalExpression().GetCleanedText();

				var memberType = new StringBuilder();

				switch (_def) {
					case TypeDef _:
						memberType.Append("type ");
						break;
					case MethodDef method: {
						memberType.Append("method ");

						if (method.IsGetter)
							memberType.Append("propertym getter ");
						else if (method.IsSetter)
							memberType.Append("propertym setter ");
						else if (method.IsAddOn)
							memberType.Append("eventm add ");
						else if (method.IsRemoveOn)
							memberType.Append("eventm remove ");
						else if (method.IsFire)
							memberType.Append("eventm fire ");
						else if (method.IsOther)
							memberType.Append("other ");
						break;
					}
					case FieldDef _:
						memberType.Append("field ");
						break;
					case PropertyDef _:
						memberType.Append("property ");
						break;
					case EventDef _:
						memberType.Append("event ");
						break;
					case ModuleDef _:
						memberType.Append("module ");
						break;
				}

				return Regex.IsMatch(memberType.ToString(), typeRegex);
			}

			public override bool VisitModuleFunction(ModuleFunctionContext context) {
				if (!(_def is IOwnerModule) && !(_def is IModule))
					return false;
				var name = context.literalExpression().GetCleanedText();
				if (_def is IModule moduleDef)
					return string.Equals(moduleDef.Name, name, StringComparison.Ordinal);
				return string.Equals(((IOwnerModule)_def).Module.Name, name, StringComparison.Ordinal);
			}

			public override bool VisitNameFunction(NameFunctionContext context) {
				var name = context.literalExpression().GetCleanedText();
				return string.Equals(_def.Name, name, StringComparison.Ordinal);
			}

			public override bool VisitNamespaceFunction(NamespaceFunctionContext context) {
				var type = (_def as IMemberDef)?.DeclaringType ?? _def as TypeDef;
				if (type == null) return false;

				var ns = "^" + context.literalExpression().GetCleanedText() + "$";

				while (type.IsNested)
					type = type.DeclaringType;

				return Regex.IsMatch(type.Namespace ?? "", ns);
			}

			public override bool VisitTrueLiteral(TrueLiteralContext context) => true;
		}
	}
}
