using System;
namespace PaprikaLang
{
	public enum TokenType
	{
		EndOfTokens,

		// litrals
		Identifier,
		NumericLiteral,
		StringLiteral,

		// keywords
		Function,

		// special punctuation & operators
		SpecialChar
	}

	public class LexerCursor
	{
		private string input;

		public int CharIdx { get; private set; }

		public LexerCursor(string input)
		{
			this.input = input;
		}

		public bool IsDone()
		{
			return CharIdx >= input.Length;
		}

		public bool CanLookAhead(int k)
		{
			return CharIdx + k < input.Length;
		}

		public void Advance()
		{
			++CharIdx;
		}

		public void AdvanceWhile(Predicate<char> test)
		{
			while (!IsDone() && test(GetChar()))
			{
				Advance();
			}
		}

		public char GetChar()
		{
			return input[CharIdx];
		}

		public char LookAhead(int k)
		{
			return input[CharIdx + k];
		}

		public string GetString(int fromIdx)
		{
			return input.Substring(fromIdx, CharIdx - fromIdx);
		}
	}

	public class Lexer
	{
		private LexerCursor cursor;

		public TokenType TokenType { get; private set; }
		public string Value { get; private set; }

		public Lexer(string input)
		{
			cursor = new LexerCursor(input);
		}

		public TokenType ScanNextToken()
		{
			// eat whitespace
			cursor.AdvanceWhile(Char.IsWhiteSpace);

			// detect end of token stream
			if (cursor.IsDone())
			{
				return EmitToken(TokenType.EndOfTokens, null);
			}

			// complex scans
			if (ScanForNumerics() ||
				ScanForStrings() ||
				ScanForIdentifiersAndKeywords())
			{
				return TokenType;
			}

			// otherwise just extract a single char symbol
			char symbol = cursor.GetChar();
			cursor.Advance();
			return EmitToken(TokenType.SpecialChar, symbol.ToString());
		}

		private bool ScanForNumerics()
		{
			int startIdx = cursor.CharIdx;
			cursor.AdvanceWhile(Char.IsDigit);

			// if no digits detected then not a numeric
			if (cursor.CharIdx == startIdx)
			{
				return false;
			}

			// permit an optional decimal point
			if (!cursor.IsDone() &&
			    cursor.GetChar() == '.' &&
			    cursor.CanLookAhead(1) &&
			    Char.IsDigit(cursor.LookAhead(1)))
			{
				cursor.Advance(); // the decimal point
				cursor.AdvanceWhile(Char.IsDigit);
			}

			EmitToken(TokenType.NumericLiteral, cursor.GetString(startIdx));
			return true;
		}

		private bool ScanForStrings()
		{
			// strings start with quotes, otherwise not a string!
			if (cursor.GetChar() != '"')
			{
				return false;
			}
			cursor.Advance();

			// find the start and end indexes
			int startIdx = cursor.CharIdx;
			cursor.AdvanceWhile(c => c != '"');

			EmitToken(TokenType.StringLiteral, cursor.GetString(startIdx));
			return true;
		}

		private bool ScanForIdentifiersAndKeywords()
		{
			// must start with letters so extract initial letters
			int startIdx = cursor.CharIdx;
			cursor.AdvanceWhile(Char.IsLetter);

			// no letters then not an identifier or a keyword
			if (cursor.CharIdx == startIdx)
			{
				return false;
			}

			// digits are now allowed so continue extracting letters and digits
			cursor.AdvanceWhile(Char.IsLetterOrDigit);

			// extact the value
			string value = cursor.GetString(startIdx);

			// determine if it's a keyword, otherwise it's an identifier
			TokenType type = TokenType.Identifier;
			switch (value)
			{
				case "func":
					type = TokenType.Function;
					break;
			}

			EmitToken(type, value);
			return true;
		}

		private TokenType EmitToken(TokenType type, string value)
		{
			TokenType = type;
			Value = value;
			return type;
		}
	}
}