using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Nephrite.Exceptions;
using Nephrite.Lexer;

namespace Nephrite.SyntaxAnalysis
{
    internal class Parser
    {
        private int current;

        private readonly ImmutableArray<Token> source;

        public Parser(ImmutableArray<Token> source)
        {
            this.source = source;
        }

        public ImmutableArray<Statement> Run()
        {
            var statements = new List<Statement>();

            while (!IsAtEnd())
            {
                var declaration = Declaration();
                if (declaration is not null)
                    statements.Add(declaration);
            }

            return statements.ToImmutableArray();
        }

        private Statement? Declaration()
        {
            return Match(TokenType.Var) ? VarDeclaration() : Statement();
        }

        private Statement VarDeclaration()
        {
            var name = Consume(TokenType.Identifier, "Expected variable name");

            Expression? initializer = null;

            if (Match(TokenType.Equal))
                initializer = Expression();

            Consume(TokenType.Semicolon, "Expected ';' after variable declaration");

            // Because variables can be assigned without a value.
            return new Var(name, initializer);
        }

        private Statement Statement()
        {
            if (Match(TokenType.Write))
                return WriteStatement();
            
            if (Match(TokenType.WriteLine))
                return WriteLineStatement();

            if (Match(TokenType.Free))
                return FreeStatement();

            if (Match(TokenType.ObjectDump))
                return ObjectDumpStatement();
            
            if (Match(TokenType.Exit))
                return ExitStatement();

            if (Match(TokenType.If))
                return IfStatement();

            if (Match(TokenType.LeftBrace))
                return new Block(BlockStatement());

            if (Match(TokenType.While))
                return WhileStatement();
            
            return ExpressionStatement();
        }

        private Statement WriteStatement()
        {
            var value = Expression();
            Consume(TokenType.Semicolon, "Expected ';' after value");
            return new Write(value);
        }

        private Statement WriteLineStatement()
        {
            var value = Expression();
            Consume(TokenType.Semicolon, "Expected ';' after value");
            return new WriteLine(value);
        }

        private Statement ExitStatement()
        {
            var value = Expression();
            Consume(TokenType.Semicolon, "Expected ';' after exit code");
            return new Exit(value);
        }

        private Statement FreeStatement()
        {
            var name = Consume(TokenType.Identifier, "Expected variable name");
            Consume(TokenType.Semicolon, "Expected ';' after variable name.");
            return new Free(name);
        }

        private Statement ObjectDumpStatement()
        {
            var value = Expression();
            Consume(TokenType.Semicolon, "Expected ';' after exit code");
            return new ObjectDump(value);
        }

        private Statement IfStatement()
        {
            Consume(TokenType.LeftParen, "Expected '(' after the if condition");
            var condition = Expression();
            Consume(TokenType.RightParen, "Expected ')' after the if condition");

            var thenBranch = Statement();
            Statement? elseBranch = null;

            if (Match(TokenType.Else))
                elseBranch = Statement();

            return new If(condition, thenBranch, elseBranch);
        }

        private Statement WhileStatement()
        {
            Consume(TokenType.LeftParen, "Expected '(' after the while condition");
            var condition = Expression();
            Consume(TokenType.RightParen, "Expected ')' after while condition");

            var body = Statement();

            return new While(condition, body);
        }

        private List<Statement> BlockStatement()
        {
            var statements = new List<Statement>();
            while (!CheckType(TokenType.RightBrace) && !IsAtEnd())
                statements.Add(Declaration());

            Consume(TokenType.RightBrace, "Expected '}' after the code block");
            return statements;
        }

        private Statement ExpressionStatement()
        {
            var expression = Expression();
            Consume(TokenType.Semicolon, "Expected ';' after expression");
            return new StatementExpression(expression);
        }

        private Expression Assignment()
        {
            var expression = Or();

            if (Match(TokenType.Equal))
            {
                var equals = Previous();
                var value = Assignment();

                if (expression is Variable variable)
                {
                    var name = variable.Name;
                    return new Assign(name, value);
                }

                Error(equals, "Invalid assignment target");
            }

            return expression;
        }

        private Expression Or()
        {
            var expression = And();

            while (Match(TokenType.Or))
            {
                var @operator = Previous();
                var right = And();
                expression = new Logical(expression, @operator, right);
            }

            return expression;
        }

        private Expression And()
        {
            var expression = Equality();

            while (Match(TokenType.And))
            {
                var @operator = Previous();
                var right = Equality();
                expression = new Logical(expression, @operator, right);
            }

            return expression;
        }

        private Expression Expression()
            => Assignment();

        private Expression Equality()
        {
            var expression = Comparison();

            while (Match(TokenType.BangEqual, TokenType.EqualEqual))
            {
                var @operator = Previous();
                var right = Comparison();
                expression = new Binary(expression, @operator, right);
            }

            return expression;
        }

        private Expression Comparison()
        {
            var expression = Term();

            while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
            {
                var @operator = Previous();
                var right = Term();
                expression = new Binary(expression, @operator, right);
            }

            return expression;
        }

        private Expression Term()
        {
            var expression = Factor();

            while (Match(TokenType.Minus, TokenType.Plus))
            {
                var @operator = Previous();
                var right = Factor();
                expression = new Binary(expression, @operator, right);
            }

            return expression;
        }

        private Expression Factor()
        {
            var expression = Unary();

            while (Match(TokenType.Slash, TokenType.Star, TokenType.Modulo))
            {
                var @operator = Previous();
                var right = Unary();
                expression = new Binary(expression, @operator, right);
            }

            return expression;
        }

        private Expression Unary()
        {
            if (Match(TokenType.Bang, TokenType.Minus))
            {
                var @operator = Previous();
                var right = Unary();
                return new Unary(@operator, right);
            }

            return Primary();
        }

        private Expression Primary()
        {
            if (Match(TokenType.False))
                return new Literal(false);

            if (Match(TokenType.True))
                return new Literal(true);

            if (Match(TokenType.Null))
                return new Literal(null);

            if (Match(TokenType.Number, TokenType.String))
                return new Literal(Previous().Value);

            if (Match(TokenType.Identifier))
                return new Variable(Previous());

            if (Match(TokenType.LeftParen))
            {
                var expression = Expression();
                Consume(TokenType.RightParen, "Expected ')' after expression");
                return new Grouping(expression);
            }

            throw Error(Peek(), "Expected expression");
        }

        private ParsingErrorException Error(Token token, string message)
            => new ParsingErrorException($"{message} (Line: {token.Line}) (Token Type: {token.Type})");

        private bool CheckType(TokenType type)
            => !IsAtEnd() && Peek().Type == type;

        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (CheckType(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private bool IsAtEnd()
            => Peek().Type == TokenType.EndOfFile;

        private Token Consume(TokenType type, string message)
            => CheckType(type) ? Advance() : throw Error(Peek(), message);

        private Token Previous()
            => source[current - 1];

        private Token Advance()
            => IsAtEnd() ? source.Last() : source[current++];

        private Token Peek()
            => source[current];

        //private void Synchronize()
        //{
        //    Advance();

        //    while (!IsAtEnd())
        //    {
        //        if (Previous().Type == TokenType.Semicolon)
        //            return;

        //        switch (Peek().Type)
        //        {
        //            case TokenType.Class:
        //            case TokenType.Fun:
        //            case TokenType.Var:
        //            case TokenType.For:
        //            case TokenType.If:
        //            case TokenType.While:
        //            case TokenType.WriteLine:
        //            case TokenType.Return:
        //                return;
        //        }

        //        Advance();
        //    }
        //}
    }
}