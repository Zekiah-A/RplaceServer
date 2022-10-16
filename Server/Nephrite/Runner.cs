using Nephrite.Exceptions;
using Nephrite.Lexer;
using Nephrite.Runtime;
using Nephrite.SyntaxAnalysis;

public class NephriteRunner
{
    private readonly Interpreter interpreter = new();
    
    public Task Execute(string source)
    {
        try
        {
            var tokens = new Scanner(source).Run();
            var statements = new Parser(tokens).Run();

            interpreter.Run(statements);
        }
        catch (Exception error) when (error is ScanningErrorException || error is ParsingErrorException || error is RuntimeErrorException)
        {
            ReportError(error.StackTrace == null ? error.Message : $"{error.Message}\n{error.StackTrace}");
        }

        return Task.CompletedTask;
    }

    private void ReportError(string message)
        => WriteConsoleColour(ConsoleColor.Red, message);

    private static void WriteConsoleColour(ConsoleColor colour, string text)
    {
        Console.ForegroundColor = colour;
        Console.Write(text);
        Console.ResetColor();
    }
}
