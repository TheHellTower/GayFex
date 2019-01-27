parser grammar ObfAttrProtectionParser;

options { tokenVocab=ObfAttrLexer; }

/*
 * Parser Rules
 */
protectionString : ( preset | ( ( preset SEP )? item ( SEP item )* ) )? SEP? ( EOL | EOF ) ;

preset      : PRESET PAREN_OPEN presetValue PAREN_CLOSE;
presetValue : IDENTIFIER;

item           : itemEnable itemName ( itemValues )?;
itemEnable     : (PLUS | MINUS)?;
itemName       : IDENTIFIER;
itemValues     : PAREN_OPEN itemValue ( SEP itemValue )* PAREN_CLOSE;
itemValue      : itemValueName EQUAL itemValueValue;
itemValueName  : IDENTIFIER;
itemValueValue : IDENTIFIER;
