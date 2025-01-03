grammar RoScript;

/* Grammar rules */

rule_set : toplevelstatement* EOF ;

toplevelstatement : MACRO IDENTIFIER LPAREN functionparam? RPAREN statementblock						# MacroDefinition
				  | EVENT IDENTIFIER statementblock														# EventDefinition
				  | functionDefinition																	# TopLevelFunctionDefinition
				  | macrocall SEMI?																		# TopLevelMacroCall
				  | IDENTIFIER LPAREN functionparam? RPAREN SEMI										# StandaloneFunction
				  ;

statement : IF LPAREN expression RPAREN block1=statementblock (ELSE block2=statementblock)?				# IfStatement
		  | FOR LPAREN asgn=expression SEMI comp=expression SEMI inc=expression RPAREN statementblock	# ForLoop
		  | WHILE LPAREN comp=expression RPAREN statementblock											# WhileLoop
		  | BREAK count=DECIMAL? SEMI																	# BreakLoop
		  | expression SEMI?																			# StatementExpression
		  | IDENTIFIER DECIMAL? COLON																	# StartSection
		  | SWITCH LPAREN expr=expression RPAREN LBRACKET switchblock* RBRACKET							# SwitchStatement
		  | RETURN expression? SEMI																		# ReturnStatement
		  ;

switchblock : CASE entity COLON statement*															# SwitchCase
		    ;

functionDefinition : IDENTIFIER LPAREN functionparam? RPAREN block1=statementblock
				   ;

statementblock : statement									#SingleStatement
			   | LBRACKET statement* RBRACKET				#StatementGroup
			   ;

functionparam : expression (COMMA expression)*
			  ;

macrocall : AT IDENTIFIER LPAREN functionparam? RPAREN (DOT function)?
		  ;
			  					  
function : IDENTIFIER LPAREN functionparam? RPAREN (DOT function)?
				(LSQUARE condition=expression RSQUARE)?
				(ARROW eventblock=statementblock)?								#FunctionCall
		 | macrocall SEMI?														#RegularMacroCall
		 ;
		  
expression : IDENTIFIER type=(INC|DEC)											#ExpressionUnary
		   | left=expression type=(MULT|DIV|MOD) right=expression				#ArithmeticMult
		   | left=expression type=(PLUS|MINUS) right=expression					#ArithmeticPlus
		   | left=expression comparison_operator right=expression				#Comparison
		   | left=expression type=(BAND|BOR) right=expression					#BitwiseAnd
		   | left=expression type=(AND|OR) right=expression						#LogicalAnd
		   | LPAREN expression RPAREN											#ArithmeticParens
		   | function															#FunctionCallExpression
		   | functionDefinition													#ExpressionFunctionDefinition
		   | PER LPAREN functionparam? RPAREN									#AreaType
		   | type=(VAR|VARSTR) assignment										#VarDeclaration
		   | type=(LOCAL|LOCALSTR) assignment									#LocalDeclaration
		   | assignment															#AssignmentExpression
		   | entity																#ExpressionEntity
		   ;

assignment : IDENTIFIER specialassignment_operator expression					#SpecialAssignment
		   | IDENTIFIER EQUALS expression										#VarAssignment
		   ;

comparison_operator: GT | GE | LT | LE | EQ | NE;

specialassignment_operator: PE | ME | DE | TE;

entity : DECIMAL	#NumericConst
	   | STRING		#StringEntity
	   | IDENTIFIER	#Variable
	   ;

/* Lexical rules : TOKENS! */

IF   : 'if' ;
ELSE : 'else';
VAR  : 'var';
END : 'end';
FOR : 'for';
WHILE : 'while';
BREAK : 'break';
MACRO : 'macro';
EVENT : 'event';
INCLUDE : '#include';
PER : '%';
SWITCH : 'switch';
CASE : 'case';
RETURN : 'return';
VARSTR : 'string';
VARINT : 'int';
LOCAL : 'local';
LOCALSTR : 'localstr';
GLOBAL : 'global';
DOT : '.';
AT : '@';

AND : '&&' ;
OR  : '||' ;

BOR : '|' ;
BAND : '&' ;

/*
TRUE  : 'true' ;
FALSE : 'false' ;
*/

ARROW : '->';

INC : '++';
DEC : '--';

MULT  : '*' ;
DIV   : '/' ;
PLUS  : '+' ;
MINUS : '-' ;
MOD   : '%' ;

GT : '>' ;
GE : '>=' ;
LT : '<' ;
LE : '<=' ;
EQ : '==' ;
NE : '!=' ;

PE : '+=' ;
ME : '-=' ;
TE : '*=' ;
DE : '/=' ;

EQUALS : '=' ;

LPAREN : '(' ;
RPAREN : ')' ;

LBRACKET : '{' ;
RBRACKET : '}' ;

LSQUARE : '[' ;
RSQUARE : ']' ;

COMMA : ',';
SEMI : ';';
COLON : ':';

STRING	:  '"' ( '\\"' | ~('"') )* '"' ;

DECIMAL : '-'?[0-9][0-9hms.]*[%]? ;
IDENTIFIER : [%a-zA-Z_][a-zA-Z_0-9]* ;

COMMENT : '//' .+? ('\n'|EOF) -> skip ;
COMMENT2 : '/*' .*? '*/' -> skip ;


WS : [ \r\t\u000C\n]+ -> skip ;


ErrorChar : . ;