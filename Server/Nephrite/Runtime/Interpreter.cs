using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Nephrite.Exceptions;
using Nephrite.Lexer;
using Nephrite.SyntaxAnalysis;

namespace Nephrite.Runtime
{
    internal class Interpreter : IExpressionVisitor<object>, IStatementVisitor<object>
    {
        private NephriteEnvironment environment;

        public Interpreter()
        {
            environment = new NephriteEnvironment();
        }

        public void Run(ImmutableArray<Statement> statements)
        {
            foreach (var statement in statements)
                Execute(statement);
        }

        public object VisitBinaryExpression(Binary binary)
        {
            var left = Evaluate(binary.Left);
            var right = Evaluate(binary.Right);

            switch (binary.Operator.Type)
            {
                case TokenType.Plus:
                {
                    return left switch
                    {
                        double d when right is double d1 => d + d1,
                        string s when right is string s1 => s + s1,
                        _ => throw new RuntimeErrorException("Operands must be two numbers or two strings.")
                    };
                }

                case TokenType.Minus:
                    {
                        CheckNumberOperands(binary.Operator, left, right);
                        return (double)left - (double)right;
                    }

                case TokenType.Slash:
                    {
                        CheckNumberOperands(binary.Operator, left, right);
                        return (double)left / (double)right;
                    }

                case TokenType.Star:
                    {
                        CheckNumberOperands(binary.Operator, left, right);
                        return (double)left * (double)right;
                    }

                case TokenType.EqualEqual:
                    return IsEqual(left, right);

                case TokenType.BangEqual:
                    return !IsEqual(left, right);

                case TokenType.Greater:
                {
                    return left switch
                    {
                        double d when right is double d1 => d > d1,
                        string s when right is string s1 => s.Length > s1.Length,
                        _ => throw new RuntimeErrorException("Operands must be two numbers or two strings.")
                    };
                }

                case TokenType.GreaterEqual:
                {
                    return left switch
                    {
                        double d when right is double d1 => d >= d1,
                        string s when right is string s1 => s.Length >= s1.Length,
                        _ => throw new RuntimeErrorException("Operands must be two numbers or two strings.")
                    };
                }

                case TokenType.Less:
                {
                    return left switch
                    {
                        double d when right is double d1 => d < d1,
                        string s when right is string s1 => s.Length < s1.Length,
                        _ => throw new RuntimeErrorException("Operands must be two numbers or two strings.")
                    };
                }

                case TokenType.LessEqual:
                {
                    return left switch
                    {
                        double d when right is double d1 => d <= d1,
                        string s when right is string s1 => s.Length <= s1.Length,
                        _ => throw new RuntimeErrorException("Operands must be two numbers or two strings.")
                    };
                }
                case TokenType.Modulo:
                    return left switch
                    {
                        double d when right is double d1 => d % d1,
                        string s when right is string s1 => int.Parse(s) % int.Parse(s1),
                        _ => throw new RuntimeErrorException("Operands must be two numbers or two strings.")
                    };
            }

            throw new RuntimeErrorException("Unknown operator.");
        }

        public object VisitGroupingExpression(Grouping grouping)
            => Evaluate(grouping.Expression);

        public object? VisitLiteralExpression(Literal literal)
            => literal.Value;

        public object VisitUnaryExpression(Unary unary)
        {
            var right = Evaluate(unary.Right);
            switch (unary.Operator.Type)
            {
                case TokenType.Bang:
                    return !IsTruthy(right);

                case TokenType.Minus:
                    CheckNumberOperands(unary.Operator, right);
                    return -(double)right;
            }

            throw new RuntimeErrorException("Unknown operator.");
        }

        public object? VisitVariableExpression(Variable variable)
            => environment.Get(variable.Name);

        public object VisitAssignExpression(Assign assign)
        {
            var value = Evaluate(assign.Value);

            environment.Assign(assign.Name, value);
            return value;
        }

        public object VisitLogicalExpression(Logical logical)
        {
            var left = Evaluate(logical.Left);

            if (logical.Operator.Type == TokenType.Or)
            {
                if (IsTruthy(left))
                    return left;
            }
            else
            {
                if (!IsTruthy(left))
                    return left;
            }

            return Evaluate(logical.Right);
        }

        private object Evaluate(Expression expression)
            => expression.Accept(this);

        public object VisitBlockStatement(Block block)
        {
            ExecuteBlock(block.Statements, new NephriteEnvironment(environment));
            return block;
        }

        public object VisitIfStatement(If @if)
        {
            if (IsTruthy(Evaluate(@if.Condition)))
                Execute(@if.ThenBranch);

            else if (@if.ElseBranch != null)
                Execute(@if.ElseBranch);

            return @if;
        }

        public object VisitWriteStatement(Write write)
        {
            var value = Evaluate(write.Expression);

            switch (value)
            {
                case null:
                    Console.Write("null");
                    break;
                case double:
                    Console.Write(value.ToString());
                    break;
                default:
                    Console.Write(value);
                    break;
            }

            return write;
        }

        public object VisitWriteLineStatement(WriteLine writeLine)
        {
            var value = Evaluate(writeLine.Expression);

            switch (value)
            {
                case null:
                    Console.WriteLine("null");
                    break;
                case double:
                    Console.WriteLine(value.ToString());
                    break;
                default:
                    Console.WriteLine(value);
                    break;
            }

            return writeLine;
        }

        public object VisitExitStatement(Exit exit)
        {
           var value = Evaluate(exit.Expression);

            switch (value)
            {
                case null:
                    Environment.Exit(0);
                    break;
                case double or bool:
                    Environment.Exit(Convert.ToInt32(value));
                    break;
                default:
                    throw new RuntimeErrorException("Statement 'exit' requires 'int' exit code.");
            }
            
            return exit;
        }

        public object VisitStatementExpression(StatementExpression statementExpression)
        {
            Evaluate(statementExpression.Expression);
            return statementExpression;
        }

        public object VisitVarStatement(Var var)
        {
            object? value = null;
            if (var.Initializer != null)
                value = Evaluate(var.Initializer);

            environment.Define(var.Name, value);
            return var;
        }

        public object VisitWhileStatement(While @while)
        {
            while (IsTruthy(Evaluate(@while.Condition)))
                Execute(@while.Body);

            return @while;
        }

        public object VisitFreeStatement(Free free)
        {
            environment.Delete(free.Name);
            return free;
        }

        public object VisitObjectDumpStatement(ObjectDump objectDump)
        {
            var sb = new StringBuilder();
            sb.AppendLine(objectDump.Expression.ToString());

            Console.WriteLine(sb);
            return objectDump;
        }

        private void ExecuteBlock(List<Statement> statements, NephriteEnvironment environment)
        {
            var previous = this.environment;
            try
            {
                this.environment = environment;

                foreach (var statement in statements)
                    Execute(statement);
            }
            finally
            {
                this.environment = previous;
            }
        }

        private void Execute(Statement statement)
            => statement.Accept(this);

        private static bool IsTruthy(object? value)
        {
            if (value == null)
                return false;

            return value is not bool b || b;
        }

        private static bool IsEqual(object? left, object? right)
        {
            return left switch
            {
                null when right == null => true,
                null => false,
                _ => left.Equals(right)
            };
        }

        private void CheckNumberOperands(Token @operator, params object[] operands)
        {
            if (operands.Any(item => item is not double))
            {
                throw new RuntimeErrorException($"Operands must be a number ({@operator.Type})");
            }
        }
    }
}
