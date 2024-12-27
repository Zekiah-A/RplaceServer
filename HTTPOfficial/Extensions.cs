using System.Text.RegularExpressions;

namespace HTTPOfficial;

public static class Extensions
{
    public delegate Regex PathMatchRegex();
    public static RouteHandlerBuilder UseMiddleware<T>(this RouteHandlerBuilder builder, WebApplication app, PathMatchRegex predicate, params object?[] args)
    {
        app.UseWhen
        (
            context => predicate().IsMatch(context.Request.Path),
            appBuilder => appBuilder.UseMiddleware<T>(args)
        );
        return builder;
    }

    public static string ToSentenceCase(this string str)
    {
        return Regex.Replace(str, "[a-z][A-Z]", match => match.Value[0] + " " + char.ToLower(match.Value[1]));
    }
}