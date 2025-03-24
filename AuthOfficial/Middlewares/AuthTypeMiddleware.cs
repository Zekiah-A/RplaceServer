using AuthOfficial.ApiModel;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using AuthOfficial.Metadatas;
using Microsoft.Extensions.Options;

namespace AuthOfficial.Middlewares;

public class AuthTypeMiddleware
{
    private readonly RequestDelegate next;
    private readonly IOptionsMonitor<AuthConfiguration> config;

    public AuthTypeMiddleware(RequestDelegate next, IOptionsMonitor<AuthConfiguration> config)
    {
        this.next = next;
        this.config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var authMetadata = endpoint?.Metadata.GetMetadata<AuthTypeMetadata>();

        if (authMetadata != null)
        {
            var typeClaim = context.User.Claims.FirstOrDefault(claim => claim.Type == "type");
            if (typeClaim is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Required claim type was not present in the provided access token", "missingClaims", "type"));
                return;
            }

            if (Enum.TryParse<AuthType>(typeClaim.Value, out var type) && !authMetadata.AuthTypeFlags.HasFlag(type))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Required claim type was not valid for the specified resource", "invalidClaims"));
                return;
            }
        }

        await next(context);
    }
}
