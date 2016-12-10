using System;
namespace PaprikaLang
{
	public enum BinaryOps
	{
		Add,
		Subtract,
		Multiply,
		Divide,
		Modulus,

		StringConcat,

		GreaterThan,
		LessThan,

		And,
		Or,

		Equals,
		NotEquals
	}

	public enum UnaryOps
	{
		Increment,
		Decrement,
		Negate
	}

	class Precedence
	{
		public static int Of(BinaryOps? op)
		{
			if (op == null)
			{
				return -1;
			}

			switch (op)
			{
				case BinaryOps.Or:
					return 1;

				case BinaryOps.And:
					return 2;

				case BinaryOps.Equals:
				case BinaryOps.NotEquals:
					return 3;

				case BinaryOps.GreaterThan:
				case BinaryOps.LessThan:
					return 4;

				case BinaryOps.StringConcat:
					return 5;
					
				case BinaryOps.Add:
				case BinaryOps.Subtract:
					return 6;

				case BinaryOps.Multiply:
				case BinaryOps.Divide:
				case BinaryOps.Modulus:
					return 7;

				
			}

			throw new Exception("Unhandled operator: " + op);
		}
	}

	class BinaryOpSymbolMatcher
	{
		private static BinaryOps? IdentifySingleCharOp(char token)
		{
			switch (token)
			{
				case '+': return BinaryOps.Add;
				case '-': return BinaryOps.Subtract;
				case '*': return BinaryOps.Multiply;
				case '/': return BinaryOps.Divide;
				case '>': return BinaryOps.GreaterThan;
				case '<': return BinaryOps.LessThan;
				case '%': return BinaryOps.Modulus;
			}

			return null;
		}

		private static BinaryOps? IdentifyMultiCharOp(char token1, char token2)
		{
			switch (string.Concat(token1, token2))
			{
				case "..": return BinaryOps.StringConcat;
				case "==": return BinaryOps.Equals;
				case "!=": return BinaryOps.NotEquals;
			}

			return null;
		}

		public static BinaryOps? AcceptBinaryOp(LexerDriver lexer)
		{
			return ParseBinaryOp(lexer, true);
		}

		public static BinaryOps? LookaheadBinaryOp(LexerDriver lexer)
		{
			return ParseBinaryOp(lexer, false);
		}


		public static BinaryOps? ParseBinaryOp(LexerDriver lexer, bool advanceLexerOnMatch)
		{
			BinaryOps? op = null;
			bool usesLookahead = false;

			if (lexer.IncomingToken == TokenType.SpecialChar)
			{
				// identify multi-char operators
				if (lexer.LookaheadToken == TokenType.SpecialChar)
				{
					op = IdentifyMultiCharOp(lexer.IncomingValue[0], lexer.LookaheadValue[0]);
					usesLookahead = op != null;
				}

				// identify single-char operators
				if (op == null)
				{
					op = IdentifySingleCharOp(lexer.IncomingValue[0]);
				}

			}
			else if (lexer.IncomingToken == TokenType.And)
			{
				op = BinaryOps.And;
			}
			else if (lexer.IncomingToken == TokenType.Or)
			{
				op = BinaryOps.Or;
			}

			// advance the lexer appropriately
			if (op != null && advanceLexerOnMatch)
			{
				lexer.AdvanceLexer();
				if (usesLookahead)
				{
					lexer.AdvanceLexer();
				}
			}

			return op;
		}
	}
}
