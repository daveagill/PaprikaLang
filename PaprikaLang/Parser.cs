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

		private ASTExpression ParseExpressionSequence(ASTExpression LHS, int minPrecedenceThreshold)
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
				ASTExpression operand = ParseOperand();

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
					operand = ParseExpressionSequence(operand, subExpressionMinPrecedence);
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
			return ParseExpressionSequence(ParseOperand(), -1);
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
			else if (lexer.IncomingToken == TokenType.Identifier)
			{
				return ParseNamedValueOrFunctionCall();
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

			// multiple assignments can take place, separated by the 'then' keyword
			IList<ASTExpression> assignmentBodies = new List<ASTExpression>();
			do
			{
				// the body of the assignment can either be a singular expression or a block
				ASTExpression assignmentBody = null;
				if (lexer.IsIncomingChar('{')) // blocks work for both first and subsequent assignment bodies
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
			while (lexer.Accept(TokenType.Then)); // keep going while there is another assignment

			return new ASTLetDef(name, type, assignmentBodies);
		}

		private ASTExpression ParseNamedValueOrFunctionCall()
		{
			// named-values and function calls start with a name
			lexer.Expect(TokenType.Identifier);
			string name = lexer.AcceptedValue;

			// function calls end with paranthesis and an arguments list
			// so if there's no parenthesis then it's a named value
			if (!lexer.AcceptChar('('))
			{
				return new ASTNamedValue(name);
			}

			// otherwise it's a function call, so parse the argument list

			IList<ASTExpression> args = new List<ASTExpression>();
			while (!lexer.AcceptChar(')'))
			{
				// expect a comma, unless it's the first arg
				if (args.Count != 0)
				{
					lexer.ExpectChar(',');
				}

				args.Add( ParseExpression() );
			}

			return new ASTFunctionCall(name, args);
		}

		private ASTIfStatement ParseIf()
		{
			lexer.Expect(TokenType.If);
			ASTExpression conditionExpr = ParseExpression();
			ASTBlock ifBody = ParseBlock();

			ASTBlock elseBody = null;
			if (lexer.Accept(TokenType.Else))
			{
				elseBody = ParseBlock();
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

			// expect a body
			ASTBlock body = ParseBlock();

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