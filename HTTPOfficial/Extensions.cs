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
}