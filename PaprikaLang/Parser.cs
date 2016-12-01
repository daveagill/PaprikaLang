using System;
using System.Collections.Generic;

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

		private ASTNode TryParseExpression()
		{
			ASTNode operand = TryParseOperand();
			return operand == null ? null : ParseExpressionSequence(operand, false);
		}

		private ASTNode TryParseOperand()
		{
			if (lexer.Accept(TokenType.NumericLiteral))
			{
				return new ASTNumeric(Double.Parse(lexer.AcceptedValue));
			}
			else if (lexer.Accept(TokenType.StringLiteral))
			{
				return new ASTString(lexer.AcceptedValue);
			}
			else if (lexer.Accept(TokenType.Identifier))
			{
				return ParseNamedValueOrFunctionCall(lexer.AcceptedValue);
			}
			else if (lexer.AcceptChar('('))
			{
				ASTNode exprSeq = TryParseExpression();
				if (exprSeq == null)
				{
					throw new Exception("Failed to parse sub-expression syntax at: " + lexer.IncomingToken + ", " + lexer.IncomingValue);
				}
				lexer.ExpectChar(')');
				return exprSeq;
			}

			return null;
		}

		private ASTNode ParseExpressionSequence(ASTNode LHS, Boolean isSubExpr)
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

				// parse the operand expression
				ASTNode operand = TryParseOperand();

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
					// the operand is the LHS of the sub-expression
					operand = ParseExpressionSequence(operand, true);
				}

				// fold RHS operand into the overall LHS expression
				LHS = new ASTBinaryOperator(leftOperator.Value, LHS, operand);

				// if we are a *sub* expression and the precedence is weaker on the right then
				// we have reached the end of our stronger-precedence sub-expression.
				// if we are a top-level expression then we must carry on to parse the full
				// expression (we are already the weakest precedence).
				if (rightPrecedence < leftPrecedence && isSubExpr)
				{
					break;
				}
			}

			// (sub)expression is fully parsed to the LHS
			return LHS;
		}

		private IList<ASTNode> ParseBlock()
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
				else
				{
					node = TryParseExpression();
				}

				if (node == null)
				{
					throw new Exception("Failed to identify syntax: " + lexer.IncomingToken + ", " + lexer.IncomingValue);
				}

				nodes.Add(node);
			}

			return nodes;
		}

		private ASTFunctionDef ParseFunctionDef()
		{
			lexer.Expect(TokenType.Function);
			lexer.Expect(TokenType.Identifier);
			string functionName = lexer.AcceptedValue;

			IList<string> args = new List<string>();
			lexer.ExpectChar('(');
			while (!lexer.AcceptChar(')'))
			{
				// expect a comma, unless it's the first arg
				if (args.Count != 0)
				{
					lexer.ExpectChar(',');
				}

				lexer.Expect(TokenType.Identifier);
				args.Add(lexer.AcceptedValue);
			}

			IList<ASTNode> functionBody = ParseBlock();

			return new ASTFunctionDef(functionName, args, functionBody);
		}

		private ASTNode ParseNamedValueOrFunctionCall(string name)
		{
			// function calls look like named-values except they end with paranthesis and an arguments list

			// if there's no parenthesis then it's a named value
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

				ASTNode argExpr = TryParseExpression();
				if (argExpr == null)
				{
					throw new Exception("Illegal expression as argument to function " + name);
				}
				args.Add(argExpr);
			}

			return new ASTFunctionCall(name, args);
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