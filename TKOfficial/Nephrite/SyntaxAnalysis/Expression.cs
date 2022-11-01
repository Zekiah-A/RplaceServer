using Nephrite.Lexer;

namespace Nephrite.SyntaxAnalysis
{
    internal abstract record Expression()
    {
        public abstract T Accept<T>(IExpressionVisitor<T> visitor);
    }

    internal interface IExpressionVisitor<T>
    {
        T VisitBinaryExpression(Binary binary);

        T VisitGroupingExpression(Grouping grouping);

        T VisitLiteralExpression(Literal literal);

        T VisitUnaryExpression(Unary unary);

        T VisitVariableExpression(Variable variable);

        T VisitAssignExpression(Assign assign);

        T VisitLogicalExpression(Logical logical);
    }

    internal record Binary(Expression Left, Token Operator, Expression Right) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitBinaryExpression(this);
    }

    internal record Grouping(Expression Expression) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitGroupingExpression(this);
    }

    internal record Literal(object? Value) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitLiteralExpression(this);
    }

    internal record Unary(Token Operator, Expression Right) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitUnaryExpression(this);
    }

    internal record Variable(Token Name) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitVariableExpression(this);
    }

    internal record Assign(Token Name, Expression Value) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitAssignExpression(this);
    }

    internal record Logical(Expression Left, Token Operator, Expression Right) : Expression
    {
        public override T Accept<T>(IExpressionVisitor<T> visitor)
            => visitor.VisitLogicalExpression(this);
    }
}
