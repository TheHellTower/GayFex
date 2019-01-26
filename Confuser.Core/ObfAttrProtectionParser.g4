parser grammar ObfAttrProtectionParser;

options { tokenVocab=ObfAttrLexer; }

/*
 * Parser Rules
 */
protectionString : ( preset | ( ( preset SEP )? item ( SEP item )* ) )? SEP? ( EOL | EOF ) ;

preset      : PRESET BRACE_OPEN presetValue BRACE_CLOSE;
presetValue : IDENTIFIER;

item           : itemEnable itemName ( itemValues )?;
itemEnable     : (PLUS | MINUS)?;
itemName       : IDENTIFIER;
itemValues     : BRACE_OPEN itemValue ( SEP itemValue )* BRACE_CLOSE;
itemValue      : itemValueName EQUAL itemValueValue;
itemValueName  : IDENTIFIER;
itemValueValue : IDENTIFIER;
