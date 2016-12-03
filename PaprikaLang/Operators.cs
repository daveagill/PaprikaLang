using System;
namespace PaprikaLang
{
	public enum BinaryOps
	{
		Add,
		Subtract,
		Multiply,
		Divide,
		StringConcat,

		GreaterThan,
		LessThan,
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
			switch (op)
			{
				case BinaryOps.Equals:
				case BinaryOps.NotEquals:
					return 1;

				case BinaryOps.GreaterThan:
				case BinaryOps.LessThan:
					return 2;
					
				case BinaryOps.Add:
				case BinaryOps.Subtract:
					return 3;

				case BinaryOps.Multiply:
				case BinaryOps.Divide:
					return 4;

				case BinaryOps.StringConcat:
					return 5;
			}

			return -1;
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
			if (lexer.IncomingToken == TokenType.SpecialChar)
			{
				BinaryOps? op;

				// identify multi-char operators
				if (lexer.LookaheadToken == TokenType.SpecialChar)
				{
					op = IdentifyMultiCharOp(lexer.IncomingValue[0], lexer.LookaheadValue[0]);
					if (op != null)
					{
						if (advanceLexerOnMatch)
						{
							lexer.AdvanceLexer();
							lexer.AdvanceLexer();
						}
						return op;
					}
				}

				// identify single-char operators
				op = IdentifySingleCharOp(lexer.IncomingValue[0]);
				if (op != null)
				{
					if (advanceLexerOnMatch)
					{
						lexer.AdvanceLexer();
					}
					return op;
				}
			}

			return null;
		}
	}
}
