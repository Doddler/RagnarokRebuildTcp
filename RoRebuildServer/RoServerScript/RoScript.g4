grammar RoScript;

/* Grammar rules */

rule_set : toplevelstatement* EOF ;

toplevelstatement : IDENTIFIER LPAREN functionparam? RPAREN block1=statementblock						# FunctionDefinition
				  | IDENTIFIER LPAREN functionparam? RPAREN SEMI										# StandaloneFunction
				  ;

statement : IF LPAREN expression RPAREN block1=statementblock (ELSE block2=statementblock)?				# IfStatement
		  | FOR LPAREN asgn=expression SEMI comp=expression SEMI inc=expression RPAREN statementblock	# ForLoop
		  | WHILE LPAREN comp=expression RPAREN statementblock											# WhileLoop
		  | BREAK SEMI																					# BreakLoop
		  | expression SEMI																				# StatementExpression
		  | IDENTIFIER DECIMAL? COLON																	# StartSection
		  | SWITCH LPAREN expr=expression RPAREN LBRACKET switchblock* RBRACKET							# SwitchStatement
		  | RETURN SEMI																					# ReturnStatement
		  ;

switchblock : CASE entity COLON statement*															# SwitchCase
		    ;

statementblock : statement									#SingleStatement
			   | LBRACKET statement* RBRACKET				#StatementGroup
			   ;

functionparam : expression (COMMA expression)*
			  ;
			  
function : IDENTIFIER LPAREN functionparam? RPAREN (DOT function)?				#FunctionCall
		 ;
		  
expression : IDENTIFIER type=(INC|DEC)											#ExpressionUnary
		   | left=expression type=(MULT|DIV|MOD) right=expression				#ArithmeticMult
		   | left=expression type=(PLUS|MINUS) right=expression					#ArithmeticPlus
		   | left=expression comparison_operator right=expression				#Comparison
		   | left=expression type=(BAND|BOR) right=expression					#BitwiseAnd
		   | left=expression type=(AND|OR) right=expression						#LogicalAnd
		   | LPAREN expression RPAREN											#ArithmeticParens
		   | function															#FunctionCallExpression
		   | PER LPAREN functionparam? RPAREN									#AreaType
		   | VAR assignment														#VarDeclaration
		   | LOCAL assignment													#LocalDeclaration
		   | assignment															#AssignmentExpression
		   | entity																#ExpressionEntity
		   ;

assignment : IDENTIFIER specialassignment_operator expression					#SpecialAssignment
		   | IDENTIFIER EQUALS expression										#VarAssignment
		   ;

comparison_operator: GT | GE | LT | LE | EQ | NE;

specialassignment_operator: PE | ME;

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
INCLUDE : '#include';
PER : '%';
SWITCH : 'switch';
CASE : 'case';
RETURN : 'return';
LOCAL : 'local';
GLOBAL : 'global';
DOT : '.';


AND : '&&' ;
OR  : '||' ;

BOR : '|' ;
BAND : '&' ;

TRUE  : 'true' ;
FALSE : 'false' ;

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

DECIMAL : '-'?[0-9]+('.'[0-9]+)? ;
IDENTIFIER : [a-zA-Z_][a-zA-Z_0-9]* ;

COMMENT : '//' .+? ('\n'|EOF) -> skip ;
COMMENT2 : '/*' .*? '*/' -> skip ;


WS : [ \r\t\u000C\n]+ -> skip ;