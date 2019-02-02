lexer grammar PatternLexer;

fragment F_PAREN_OPEN  : '(';
fragment F_PAREN_CLOSE : ')';

fragment F_SINGLE_QUOTE_LITERAL : '\'' ( ( '\\\'' ) | ~'\'' )* '\'';
fragment F_DOUBLE_QUOTE_LITERAL : '"' ( ( '\\"' ) | ~'"' )* '"';

PAREN_OPEN  : F_PAREN_OPEN;
PAREN_CLOSE : F_PAREN_CLOSE;

AND : 'and';
NOT : 'not';
OR  : 'or';

DECL_TYPE       : 'decl-type';
FULL_NAME       : 'full-name';
HAS_ATTR        : 'has-attr';
INHERITS        : 'inherits';
IS_PUBLIC       : 'is-public';
IS_TYPE         : 'is-type';
MATCH           : 'match';
MATCH_NAME      : 'match-name';
MATCH_TYPE_NAME : 'match-type-name';
MEMBER_TYPE     : 'member-type';
MODULE          : 'module';
NAME            : 'name';
NAMESPACE       : 'namespace';

TRUE  : 'true';
FALSE : 'false';

LITERAL : F_SINGLE_QUOTE_LITERAL | F_DOUBLE_QUOTE_LITERAL;

WS  : ( ' ' | '\t' ) -> skip;
EOL : ( '\r\n' | '\r' | '\n' ) -> skip;
