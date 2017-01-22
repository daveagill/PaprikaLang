using System;
using System.Text.RegularExpressions;

namespace PaprikaLang
{
	public enum TokenType
	{
		EndOfTokens,

		// literals
		Identifier,
		NumericLiteral,
		StringLiteral,

		// keywords
		Type,
		Function,
		Let,
		Where,
		Foreach,
		In,
		Do,
		If,
		Then,
		Else,
		And,
		Or,

		// special punctuation & operators
		Arrow,
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
			// ignore whitespace and comments
			EatWhitespaceAndComments();

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

		private void EatWhitespaceAndComments()
		{
			while (true)
			{
				int startIdx = cursor.CharIdx;

				// eat whitespace
				cursor.AdvanceWhile(Char.IsWhiteSpace);

				// detect a comment with //
				if (!cursor.IsDone() &&
					cursor.GetChar() == '/' &&
					cursor.CanLookAhead(1) &&
				    cursor.LookAhead(1) == '/')
				{
					// eat the whole line up till the newline
					cursor.AdvanceWhile(c => c != '\n');
				}

				// if nothing has been eaten then we can exit
				if (cursor.CharIdx == startIdx)
				{
					return;
				}
			}
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

			string str = cursor.GetString(startIdx);
			cursor.Advance(); // eat the closing quote

			// apply escapements (\n, \t etc)
			str = Regex.Unescape(str);

			EmitToken(TokenType.StringLiteral, str);


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
				case "type":
					type = TokenType.Type;
					break;
					
				case "func":
					type = TokenType.Function;
					break;

				case "let":
					type = TokenType.Let;
					break;

				case "where":
					type = TokenType.Where;
					break;

				case "foreach":
					type = TokenType.Foreach;
					break;

				case "in":
					type = TokenType.In;
					break;

				case "do":
					type = TokenType.Do;
					break;

				case "if":
					type = TokenType.If;
					break;

				case "then":
					type = TokenType.Then;
					break;

				case "else":
					type = TokenType.Else;
					break;

				case "and":
					type = TokenType.And;
					break;

				case "or":
					type = TokenType.Or;
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