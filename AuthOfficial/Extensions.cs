using System.Security.Claims;
using System.Text.RegularExpressions;
using AuthOfficial.DataModel;
using AuthOfficial.Metadatas;

namespace AuthOfficial;

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

    public static TBuilder RequireAuthType<TBuilder>(this TBuilder builder, AuthType authTypeFlags) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(new AuthTypeMetadata(authTypeFlags));
        });
        
        return builder;
    }
    
    public static TBuilder RequireClaims<TBuilder>(this TBuilder builder, params string[] types) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(new ClaimsMetadata(types));
        });
        
        return builder;
    }

    public static TBuilder RateLimit<TBuilder>(this TBuilder builder, TimeSpan timeSpan) where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(new RateLimitMetadata(timeSpan));
        });
        return builder;
    }

    public static T FindFirstAs<T>(this IEnumerable<Claim> claims, string type) where T : notnull
    {
        var claim = claims.FirstOrDefault(claim => claim.Type == type);
        if (claim == null)
        {
            throw new InvalidOperationException($"Claim of type '{type}' was not found.");
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)claim.Value;
        }

        if (typeof(T) == typeof(int))
        {
            if (int.TryParse(claim.Value, out var intValue))
            {
                return (T)(object)intValue;
            }
            
            throw new FormatException($"Claim value '{claim.Value}' cannot be converted to an integer.");
        }

        if (typeof(T).IsEnum)
        {
            if (Enum.TryParse(typeof(T), claim.Value, true, out var enumValue) && enumValue != null)
            {
                return (T)enumValue;
            }
            
            throw new ArgumentException($"Claim value '{claim.Value}' is not a valid value for enum type '{typeof(T).Name}'.");
        }

        throw new NotSupportedException($"Conversion to type '{typeof(T).Name}' is not supported.");
    }

    public static string ToSentenceCase(this string str)
    {
        return Regex.Replace(str, "[a-z][A-Z]", match => match.Value[0] + " " + char.ToLower(match.Value[1]));
    }
}
