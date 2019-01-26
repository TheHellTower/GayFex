parser grammar ObfAttrPackerParser;

options { tokenVocab=ObfAttrLexer; }

/*
 * Parser Rules
 */
packerString : packer EOL ;

packer : itemName ( itemValues )?;

itemName       : WS* IDENTIFIER WS* ;
itemValues     : BRACE_OPEN WS* itemValue ( SEP itemValue )* WS* BRACE_CLOSE;
itemValue      : itemValueName WS* EQUAL WS* itemValueValue WS*;
itemValueName  : IDENTIFIER;
itemValueValue : IDENTIFIER;
