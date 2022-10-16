using System.Collections.Generic;

namespace Nephrite.Lexer
{
    internal static class ReservedIdentifiers
    {
        public static readonly IReadOnlyDictionary<string, TokenType> Keywords =
            new Dictionary<string, TokenType>
            {
                { "and", TokenType.And },
                { "class", TokenType.Class },
                { "else", TokenType.Else },
                { "false", TokenType.False },
                { "for", TokenType.For },
                { "fun", TokenType.Fun },
                { "if", TokenType.If },
                { "null", TokenType.Null },
                { "or", TokenType.Or },
                { "write", TokenType.Write },
                { "writeLine", TokenType.WriteLine},
                { "return", TokenType.Return },
                { "super", TokenType.Super },
                { "this", TokenType.This },
                { "true", TokenType.True },
                { "var", TokenType.Var },
                { "while", TokenType.While },
                { "exit", TokenType.Exit }
            };

        public static readonly IReadOnlyDictionary<char, TokenType> SingleCharacters =
            new Dictionary<char, TokenType>
            {
                { '(', TokenType.LeftParen },
                { ')', TokenType.RightParen },
                { '{', TokenType.LeftBrace },
                { '}', TokenType.RightBrace },
                { ',', TokenType.Comma },
                { '.', TokenType.Dot },
                { '-', TokenType.Minus },
                { '+', TokenType.Plus },
                { ';', TokenType.Semicolon },
                { '*', TokenType.Star },
                { '%', TokenType.Modulo }
            };
    }
}
