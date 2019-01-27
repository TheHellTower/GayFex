parser grammar PatternParser;

options { tokenVocab=PatternLexer; }

pattern : function
        | pattern AND pattern
        | pattern OR pattern
        | NOT pattern
        | PAREN_OPEN pattern PAREN_CLOSE;

function : declTypeFunction
         | fullNameFunction
         | hasAttrFunction
         | inheritsFunction
         | isPublicFunction
         | isTypeFunction
         | matchFunction
         | matchNameFunction
         | matchTypeNameFunction
         | memberTypeFunction
         | moduleFunction
         | nameFunction
         | namespaceFunction
         | trueLiteral
         | falseLiteral;

declTypeFunction      : DECL_TYPE       PAREN_OPEN literalExpression PAREN_CLOSE;
fullNameFunction      : FULL_NAME       PAREN_OPEN literalExpression PAREN_CLOSE;
hasAttrFunction       : HAS_ATTR        PAREN_OPEN literalExpression PAREN_CLOSE;
inheritsFunction      : INHERITS        PAREN_OPEN literalExpression PAREN_CLOSE;
isPublicFunction      : IS_PUBLIC;
isTypeFunction        : IS_TYPE         PAREN_OPEN literalExpression PAREN_CLOSE;
matchFunction         : MATCH           PAREN_OPEN literalExpression PAREN_CLOSE;
matchNameFunction     : MATCH_NAME      PAREN_OPEN literalExpression PAREN_CLOSE;
matchTypeNameFunction : MATCH_TYPE_NAME PAREN_OPEN literalExpression PAREN_CLOSE;
memberTypeFunction    : MEMBER_TYPE     PAREN_OPEN literalExpression PAREN_CLOSE;
moduleFunction        : MODULE          PAREN_OPEN literalExpression PAREN_CLOSE;
nameFunction          : NAME            PAREN_OPEN literalExpression PAREN_CLOSE;
namespaceFunction     : NAMESPACE       PAREN_OPEN literalExpression PAREN_CLOSE;

literalExpression : LITERAL;
trueLiteral       : TRUE;
falseLiteral      : FALSE;
