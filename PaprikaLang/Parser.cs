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
			IList<ASTFunctionDef> funcDefs = new List<ASTFunctionDef>();
			while (lexer.IncomingToken != TokenType.EndOfTokens)
			{
				ASTFunctionDef funcDef = ParseFunctionDef();
				funcDefs.Add(funcDef);
			}
			return new ASTModule(funcDefs);
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

		private ASTExpression ParseExpression()
		{
			return ParseExpressionSequence(ParseOperand(), -1);
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
			IList<ASTBlock> assignmentBodies = new List<ASTBlock>();
			do
			{
				// the body of the assignment can either be a simple expression or a whole block
				ASTBlock assignmentBody;
				if (lexer.IsIncomingChar('{'))
				{
					assignmentBody = ParseBlock();
				}
				else
				{
					assignmentBody = new ASTBlock(
						new ASTNode[] { ParseExpression() });
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

			IList<ASTNode> args = new List<ASTNode>();
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
			ASTNode conditionExpr = ParseExpression();
			ASTBlock ifBody = ParseBlock();

			ASTBlock elseBody = null;
			if (lexer.Accept(TokenType.Else))
			{
				elseBody = ParseBlock();
			}

			return new ASTIfStatement(conditionExpr, ifBody, elseBody);
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

		public static ParseResult Parse(string input)
		{
			Parser p = new Parser(input);
			ParseResult r = new ParseResult();
			r.RootNode = p.ParseModule();
			return r;
		}
	}
}