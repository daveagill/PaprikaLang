using System;
namespace PaprikaLang
{
	public class LexerDriver
	{
		private Lexer lexer;

		public TokenType AcceptedToken, IncomingToken, LookaheadToken;
		public string AcceptedValue, IncomingValue, LookaheadValue;

		public LexerDriver(string input)
		{
			this.lexer = new Lexer(input);

			// drive the lexer so that we have 1 token of lookahead (k=1)
			lexer.ScanNextToken();
			AdvanceLexer();
		}

		public void AdvanceLexer()
		{
			AcceptedToken = IncomingToken;
			AcceptedValue = IncomingValue;

			IncomingToken = lexer.TokenType;
			IncomingValue = lexer.Value;

			lexer.ScanNextToken();

			LookaheadToken = lexer.TokenType;
			LookaheadValue = lexer.Value;
		}

		public bool Accept(TokenType type)
		{
			if (IncomingToken == type)
			{
				AdvanceLexer();
				return true;
			}
			return false;
		}

		public void Expect(TokenType type)
		{
			if (!Accept(type))
			{
				Error("Unexpected " + IncomingToken + " token '" + IncomingValue + "'. Expected " + type);
			}
		}

		public bool IsIncomingChar(char c)
		{
			return IncomingToken == TokenType.SpecialChar && IncomingValue.Length == 1 && IncomingValue[0] == c;
		}

		public bool AcceptChar(char c)
		{
			if (IsIncomingChar(c))
			{
				AdvanceLexer();
				return true;
			}
			return false;
		}

		public void ExpectChar(char c)
		{
			if (!AcceptChar(c))
			{
				Error("Unexpected character '" + IncomingValue + "'. Expected " + c);
			}
		}

		private void Error(string message)
		{
			throw new Exception(message);
		}
	}
}
