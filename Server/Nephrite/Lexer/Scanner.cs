using System.Collections.Generic;
using System.Collections.Immutable;
using Nephrite.Exceptions;

namespace Nephrite.Lexer
{
    internal class Scanner
    {
        private int line = 1;
        private int start;
        private int current;

        private readonly string source;
        private readonly List<Token> tokens;

        public Scanner(string source)
        {
            this.source = source;
            tokens = new List<Token>();
        }

        public ImmutableArray<Token> Run()
        {
            while (!IsAtEnd())
            {
                start = current;
                Scan();
            }

            AddToken(TokenType.EndOfFile);
            return tokens.ToImmutableArray();
        }

        private bool Match(char expected)
        {
            if (Peek() == expected)
            {
                current++;
                return true;
            }

            return false;
        }

        private bool IsAtEnd()
            => current >= source.Length;

        private char Advance()
            => IsAtEnd() ? '\0' : source[current++];

        private char Peek(int distance = 0)
            => IsAtEnd() ? '\0' : source[current + distance];

        private void Scan()
        {
            var character = Advance();
            switch (character)
            {
                case '\n': line++; break;
                case ' ': break;
                case '\r': break;
                case '\t': break;

                case '!':
                    AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang);
                    break;

                case '=':
                    AddToken(Match('=') ? TokenType.EqualEqual : TokenType.Equal);
                    break;

                case '<':
                    AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less);
                    break;

                case '>':
                    AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater);
                    break;

                case '%': AddToken(TokenType.Modulo); 
                    break;

                case >= '0' and <= '9':
                    {
                        while (char.IsDigit(Peek()))
                            Advance();

                        if (Peek() == '.' && char.IsDigit(Peek(1)))
                        {
                            Advance();

                            while (char.IsDigit(Peek()))
                                Advance();
                        }

                        AddToken(TokenType.Number, double.Parse(source[start..current]));
                        break;
                    }

                case >= 'a' and <= 'z' or '_':
                    {
                        while (char.IsDigit(Peek()) || char.IsLetter(Peek()) || Match('_'))
                            Advance();

                        var identifier = source[start..current];

                        if (ReservedIdentifiers.Keywords.TryGetValue(identifier, out var identifierType))
                            AddToken(identifierType, identifier);

                        else
                            AddToken(TokenType.Identifier, identifier);
                        break;
                    }

                case '"':
                    {
                        while (Peek() != '"' && !IsAtEnd())
                        {
                            if (Peek() == '\n')
                                line++;

                            Advance();
                        }

                        if (IsAtEnd())
                            Error("Unterminated string");

                        // The closing quote.
                        Advance();

                        var content = source[(start + 1)..(current - 1)];
                        AddToken(TokenType.String, content);
                        break;
                    }

                case '/':
                    {
                        if (Match('/'))
                        {
                            while (Peek() != '\n' && !IsAtEnd())
                                Advance();

                            break;
                        }

                        AddToken(TokenType.Slash);
                        break;
                    }
                    
                default:
                    {
                        if (ReservedIdentifiers.SingleCharacters.TryGetValue(character, out var characterType))
                            AddToken(characterType, character);

                        else
                            Error($"Unknown character '{character}'");
                        break;
                    }
            }
        }

        private void Error(string message)
            => throw new ScanningErrorException(message);

        private void AddToken(TokenType type, object? value = null)
            => tokens.Add(new Token(type, value, line));
    }
}
