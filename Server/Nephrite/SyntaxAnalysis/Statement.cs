using System.Collections.Generic;
using Nephrite.Lexer;

namespace Nephrite.SyntaxAnalysis
{
    internal abstract record Statement()
    {
        public abstract T Accept<T>(IStatementVisitor<T> visitor);
    }

    internal interface IStatementVisitor<T>
    {
        T VisitBlockStatement(Block block);

        T VisitStatementExpression(StatementExpression statementExpression);

        T VisitIfStatement(If @if);

        T VisitWriteStatement(Write write);

        T VisitWriteLineStatement(WriteLine writeLine);

        T VisitExitStatement(Exit exit);

        T VisitVarStatement(Var var);

        T VisitWhileStatement(While @while);
    }

    internal record Block(List<Statement> Statements) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitBlockStatement(this);
    }

    internal record StatementExpression(Expression Expression) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitStatementExpression(this);
    }

    internal record If(Expression Condition, Statement ThenBranch, Statement? ElseBranch) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitIfStatement(this);
    }

    internal record Write(Expression Expression) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitWriteStatement(this);

    }

    internal record WriteLine(Expression Expression) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitWriteLineStatement(this);
    }

    internal record Exit(Expression Expression) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitExitStatement(this);
    }

    internal record Var(Token Name, Expression? Initializer) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitVarStatement(this);
    }

    internal record While(Expression Condition, Statement Body) : Statement
    {
        public override T Accept<T>(IStatementVisitor<T> visitor)
            => visitor.VisitWhileStatement(this);
    }
}
