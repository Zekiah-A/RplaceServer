namespace Nephrite.Lexer
{
    internal enum TokenType
    {
        // Single-character tokens.
        LeftParen, RightParen, LeftBrace, RightBrace,
        Comma, Dot, Minus, Plus, Semicolon, Slash, Star, Modulo,

        // One or two character tokens.
        Bang, BangEqual,
        Equal, EqualEqual,
        Greater, GreaterEqual,
        Less, LessEqual,

        // Literals.
        Identifier, String, Number,

        // Keywords.
        And, Class, Else, False, Fun, For, If, Null, Or,
        Write, WriteLine, Return, Super, This, True, Var, While, Exit,

        EndOfFile
    }
}
