lexer grammar ObfAttrLexer;

fragment A : ( 'a' | 'A' );
fragment B : ( 'b' | 'B' );
fragment C : ( 'c' | 'C' );
fragment D : ( 'd' | 'D' );
fragment E : ( 'e' | 'E' );
fragment F : ( 'f' | 'F' );
fragment G : ( 'g' | 'G' );
fragment H : ( 'h' | 'H' );
fragment I : ( 'i' | 'I' );
fragment J : ( 'j' | 'J' );
fragment K : ( 'k' | 'K' );
fragment L : ( 'l' | 'L' );
fragment M : ( 'm' | 'M' );
fragment N : ( 'n' | 'N' );
fragment O : ( 'o' | 'O' );
fragment P : ( 'p' | 'P' );
fragment Q : ( 'q' | 'Q' );
fragment R : ( 'r' | 'R' );
fragment S : ( 's' | 'S' );
fragment T : ( 't' | 'T' );
fragment U : ( 'u' | 'U' );
fragment V : ( 'v' | 'V' );
fragment W : ( 'w' | 'W' );
fragment X : ( 'x' | 'X' );
fragment Y : ( 'y' | 'Y' );
fragment Z : ( 'z' | 'Z' );

fragment F_PLUS        : '+';
fragment F_MINUS       : '-';
fragment F_EQUAL       : '=';
fragment F_PAREN_OPEN  : '(';
fragment F_PAREN_CLOSE : ')';

fragment F_COMMA       : ',';
fragment F_SEMICOLON   : ';';

fragment F_SINGLE_QUOTE : '\'';
fragment F_ESCAPE_CHAR  : '\\';

// This looks ugly, but it's due to this bug: https://github.com/antlr/antlr4/issues/70
fragment F_NO_CONTROL_CHAR       : ~( '+' | '-' | '=' | '(' | ')' | ',' | ';' );
fragment F_NO_QUOTE_CONTROL_CHAR : ~( '+' | '-' | '=' | '(' | ')' | ',' | ';' | '\'' );

fragment F_ID_STRING      : F_NO_QUOTE_CONTROL_CHAR F_NO_CONTROL_CHAR*;
fragment F_ESCAPED_STRING : F_SINGLE_QUOTE ( ( F_ESCAPE_CHAR F_SINGLE_QUOTE ) | ~'\'' )* F_SINGLE_QUOTE;

PLUS        : F_PLUS;
MINUS       : F_MINUS;
EQUAL       : F_EQUAL;
PAREN_OPEN  : F_PAREN_OPEN;
PAREN_CLOSE : F_PAREN_CLOSE;

PRESET     : P R E S E T;
IDENTIFIER : F_ID_STRING | F_ESCAPED_STRING;

SEP : ( F_SEMICOLON | F_COMMA );
WS  : ( ' ' | '\t' ) -> skip;
EOL : ( '\r\n' | '\r' | '\n' ) -> skip;
