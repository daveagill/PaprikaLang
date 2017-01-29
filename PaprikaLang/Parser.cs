using System;
using System.Collections.Generic;
using System.Linq;

namespace PaprikaLang
{
	public class ParseResult
	{
		public ASTModule RootNode { get; set; }
		public List<string> Errors { get; } = new List<string>();
	}

	public class Parser
	{
		private LexerDriver lexer;

		private Parser(string input)
		{
			lexer = new LexerDriver(input);
		}

		private ASTModule ParseModule()
		{
			IList<ASTNode> defs = new List<ASTNode>();
			while (lexer.IncomingToken != TokenType.EndOfTokens)
			{
				ASTNode node = null;

				if (lexer.IncomingToken == TokenType.Type)
				{
					node = ParseTypeDef();
				}
				else if (lexer.IncomingToken == TokenType.Function)
				{
					node = ParseFunctionDef();
				}

				if (node == null) // this is actually a parse bug
				{
					throw new Exception("Unhandled syntax in block: " + lexer.IncomingToken + ", " + lexer.IncomingValue);
				}

				defs.Add(node);
			}

			return new ASTModule(new ASTBlock(defs));
		}

		private ASTTypeNameParts ParseTypeNameParts()
		{
			lexer.Expect(TokenType.Identifier);
			string typename = lexer.AcceptedValue;

			// look for generic arguments
			IList<ASTTypeNameParts> genericArgs = new List<ASTTypeNameParts>();
			if (lexer.AcceptChar('<'))
			{
				while (!lexer.AcceptChar('>'))
				{
					genericArgs.Add(ParseTypeNameParts());
				}
			}

			return new ASTTypeNameParts(typename, genericArgs);
		}

		private ASTExpression ParseOperatorSequence(ASTExpression LHS, int minPrecedenceThreshold)
		{
			// keep going while there are still operators in the sequence
			while (true)
			{
				// get the left operator, if there is no such operator then we've reached the end
				BinaryOps? leftOperator = BinaryOpSymbolMatcher.AcceptBinaryOp(lexer);
				if (leftOperator == null)
				{
					break;
				}

				// parse the next operand
				ASTExpression operand = ParseOperandOrMemberAccess();

				// lookahead for an optional right operator
				BinaryOps? rightOperator = BinaryOpSymbolMatcher.LookaheadBinaryOp(lexer);

				// determine which side owns the operand based on operator precedence.
				// if the operator is stronger on the right then recurse to determine
				// the entire sub-expression that owns the operand and then treat it
				// as the operand itself
				int leftPrecedence = Precedence.Of(leftOperator);
				int rightPrecedence = Precedence.Of(rightOperator);
				if (rightPrecedence > leftPrecedence)
				{
					int subExpressionMinPrecedence = leftPrecedence;
					// the operand is the LHS of the sub-expression
					operand = ParseOperatorSequence(operand, subExpressionMinPrecedence);
				}

				// fold RHS operand into the overall LHS expression
				LHS = new ASTBinaryOperator(leftOperator.Value, LHS, operand);

				// keep going whilst our subexpression has higher precedence operators
				// than our predecessor expression. Once the RHS is weaker than the threshold
				// then we must stop parsing since our subexpression will be the LHS and
				// folded into a binop at a higher recursive level.
				if (rightPrecedence < minPrecedenceThreshold)
				{
					break;
				}
			}

			// (sub)expression is fully parsed to the LHS
			return LHS;
		}

		private ASTExpression ParseExpression()
		{
			return ParseOperatorSequence(ParseOperandOrMemberAccess(), -1);
		}

		// A 'then expression' is an extended form of expression
		private ASTExpression ParseAnExtendedThenExpression()
		{
			if (lexer.IncomingToken == TokenType.If) // possible conditional assignment
			{
				return ParseIf();
			}
			else if (lexer.IncomingToken == TokenType.Foreach) // loop/repeating assignment
			{
				return ParseForeachAssignment();
			}

			// must be a standard-form expression
			return ParseExpression();
		}

		private ASTExpression ParseOperandOrMemberAccess()
		{
			ASTExpression expr = ParseOperand();

			// resolve member accesses via the dot operator
			// but don't confuse it with other dot-like operators!
			// the pattern is: .(dot)Identifier
			while (lexer.IsIncomingChar('.') && lexer.LookaheadToken == TokenType.Identifier)
			{
				lexer.ExpectChar('.');
				lexer.Expect(TokenType.Identifier);
				string dataMember = lexer.AcceptedValue;

				// fold into the expression as a member-access node
				expr = new ASTMemberAccess(expr, dataMember);
			}

			return expr;
		}

		private ASTExpression ParseOperand()
		{
			if (lexer.Accept(TokenType.NumericLiteral))
			{
				return new ASTNumeric(Double.Parse(lexer.AcceptedValue));
			}
			else if (lexer.Accept(TokenType.StringLiteral))
			{
				return new ASTString(lexer.AcceptedValue);
			}
			else if (lexer.Accept(TokenType.BooleanTrueLiteral))
			{
				return new ASTBoolean(true);
			}
			else if (lexer.Accept(TokenType.BooleanFalseLiteral))
			{
				return new ASTBoolean(false);
			}
			else if (lexer.IncomingToken == TokenType.Identifier)
			{
				return ParseNamedValueOrFunctionCallOrTypeConstruction();
			}
			else if (lexer.IncomingToken == TokenType.If)
			{
				return ParseIf();
			}
			else if (lexer.IsIncomingChar('['))
			{
				return ParseListLiteral();
			}
			else if (lexer.AcceptChar('('))
			{
				ASTExpression exprSeq = ParseExpression();
				lexer.ExpectChar(')');
				return exprSeq;
			}

			throw new Exception("Unable to parse operand at: " + lexer.IncomingToken + ", " + lexer.IncomingValue);
		}

		private ASTBlock ParseBlock()
		{
			lexer.ExpectChar('{');

			IList<ASTNode> nodes = new List<ASTNode>();
			while (!lexer.AcceptChar('}'))
			{
				ASTNode node = null;

				if (lexer.IncomingToken == TokenType.Function)
				{
					node = ParseFunctionDef();
				}
				else if (lexer.IncomingToken == TokenType.Let)
				{
					node = ParseLetDef();
				}
				else
				{
					node = ParseExpression();
				}

				if (node == null) // this is actually a parse bug
				{
					throw new Exception("Unhandled syntax in block: " + lexer.IncomingToken + ", " + lexer.IncomingValue);
				}

				nodes.Add(node);
			}

			return new ASTBlock(nodes);
		}

		private ASTExpression ParseBlockOrExpression()
		{
			if (lexer.IsIncomingChar('{'))
			{
				return ParseBlock();
			}
			return ParseExpression();
		}

		private ASTFunctionDef ParseFunctionDef()
		{
			lexer.Expect(TokenType.Function);
			lexer.Expect(TokenType.Identifier);
			string functionName = lexer.AcceptedValue;

			// parse parameters
			IList<ASTFunctionDef.ASTParam> args = new List<ASTFunctionDef.ASTParam>();
			lexer.ExpectChar('(');
			while (!lexer.AcceptChar(')'))
			{
				// expect a comma, unless it's the first arg
				if (args.Count != 0)
				{
					lexer.ExpectChar(',');
				}

				// expect parameter name
				lexer.Expect(TokenType.Identifier);
				string paramName = lexer.AcceptedValue;

				// expect parameter type
				ASTTypeNameParts paramType = ParseTypeNameParts();

				args.Add(new ASTFunctionDef.ASTParam(paramName, paramType));
			}

			// parse arrow (->) then return-type
			lexer.ExpectChar('-');
			lexer.ExpectChar('>');
			ASTTypeNameParts returnType = ParseTypeNameParts();

			ASTBlock functionBody = ParseBlock();

			return new ASTFunctionDef(functionName, args, functionBody, returnType);
		}

		private ASTLetDef ParseLetDef()
		{
			lexer.Expect(TokenType.Let);

			// expect a name
			lexer.Expect(TokenType.Identifier);
			string name = lexer.AcceptedValue;

			// expect a type
			ASTTypeNameParts type = ParseTypeNameParts();

			// expect assignment symbol
			lexer.ExpectChar('=');

			// multiple assignments can take place, separated by the 'where' keyword
			IList<ASTExpression> assignmentBodies = new List<ASTExpression>();
			do
			{
				// the body of the assignment can either be a singular expression or a block
				ASTExpression assignmentBody = null;
				if (lexer.IsIncomingChar('{')) // blocks work for all assignment bodies
				{
					assignmentBody = ParseBlock();
				}
				else // must be a singular expression form...
				{
					if (assignmentBodies.Count == 0) // the first assignment can only be a regular expression
					{
						assignmentBody = ParseExpression();
					}
					else // subsequent assignments could be of the extended variety
					{
						assignmentBody = ParseAnExtendedThenExpression();
					}
				}

				assignmentBodies.Add(assignmentBody);
			}
			while (lexer.Accept(TokenType.Where)); // keep going while there is another assignment

			return new ASTLetDef(name, type, assignmentBodies);
		}

		private ASTExpression ParseNamedValueOrFunctionCallOrTypeConstruction()
		{
			// named-values, function calls and type constructions all start with a name
			lexer.Expect(TokenType.Identifier);
			string name = lexer.AcceptedValue;

			// use lookahead to classify the parse
			bool isFunctionCall = lexer.IsIncomingChar('(');
			bool isTypeConstruction = lexer.IsIncomingChar('{');
			bool isNamedValue = !isFunctionCall && !isTypeConstruction;

			// if it's a named value then we're done
			if (isNamedValue)
			{
				return new ASTNamedValue(name);
			}

			// otherwise we need to parse the argument list

			if (isFunctionCall)
			{
				lexer.ExpectChar('(');
				IList<ASTExpression> args = ParseCallArgList(')');
				return new ASTFunctionCall(name, args);
			}
			else if (isTypeConstruction)
			{
				lexer.ExpectChar('{');
				IList<ASTExpression> args = ParseCallArgList('}');
				return new ASTTypeConstruction(name, args);
			}

			// this implies an unhandled code-path
			throw new Exception("Unhandled parse path. This is unexpected.");
		}

		private IList<ASTExpression> ParseCallArgList(char terminatingChar)
		{
			IList<ASTExpression> args = new List<ASTExpression>();
			while (!lexer.AcceptChar(terminatingChar))
			{
				// expect a comma, unless it's the first arg
				if (args.Count != 0)
				{
					lexer.ExpectChar(',');
				}

				args.Add(ParseExpression());
			}
			return args;
		}

		private ASTIfStatement ParseIf()
		{
			lexer.Expect(TokenType.If);
			ASTExpression conditionExpr = ParseExpression();

			lexer.Expect(TokenType.Then);
			ASTExpression ifBody = ParseBlockOrExpression();

			ASTExpression elseBody = null;
			if (lexer.Accept(TokenType.Else))
			{
				elseBody = ParseBlockOrExpression();
			}

			return new ASTIfStatement(conditionExpr, ifBody, elseBody);
		}

		private ASTForeachAssignment ParseForeachAssignment()
		{
			lexer.Expect(TokenType.Foreach);

			// expect a name for the element var
			lexer.Expect(TokenType.Identifier);
			string elementName = lexer.AcceptedValue;

			// expect a type for the element var
			ASTTypeNameParts elementType = ParseTypeNameParts();

			lexer.Expect(TokenType.In);

			// expect a range to iterate over
			ASTExpression range = ParseExpression();

			lexer.Expect(TokenType.Do);

			// expect a body
			ASTExpression body = ParseBlockOrExpression();

			return new ASTForeachAssignment(elementName, elementType, range, body);
		}

		private ASTList ParseListLiteral()
		{
			lexer.ExpectChar('[');

			ASTExpression from = ParseExpression();

			// 'to' is a context sensitive keyword so parse it as an identifier
			if (lexer.IncomingValue != "to")
			{
				throw new Exception("Expected 'to' keyword in list literal expression, but got: " + lexer.AcceptedValue);
			}
			lexer.Expect(TokenType.Identifier);

			ASTExpression to = ParseExpression();

			// parse an optional step
			ASTExpression step = null;

			// 'step' is a context sensitive keyword so parse it as an identifier
			if (lexer.IncomingValue == "step")
			{
				lexer.Expect(TokenType.Identifier);
				step = ParseExpression();
			}

			lexer.ExpectChar(']');

			return new ASTList(from, to, step);
		}

		public ASTTypeDef ParseTypeDef()
		{
			lexer.Expect(TokenType.Type);

			lexer.Expect(TokenType.Identifier);
			string typename = lexer.AcceptedValue;

			lexer.ExpectChar('{');

			IList<ASTTypeDef.ASTField> fields = new List<ASTTypeDef.ASTField>();
			while (!lexer.AcceptChar('}'))
			{
				lexer.Expect(TokenType.Identifier);
				string fieldName = lexer.AcceptedValue;

				ASTTypeNameParts fieldType = ParseTypeNameParts();

				fields.Add(new ASTTypeDef.ASTField(fieldName, fieldType));
			}

			return new ASTTypeDef(typename, fields);
		}

		public static ParseResult Parse(string input)
		{
			Parser p = new Parser(input);
			ParseResult r = new ParseResult();
			r.RootNode = p.ParseModule();
			return r;
		}
	}
}