using Microsoft.Extensions.Options;
using HTTPOfficial.Metadatas;
using HTTPOfficial.ApiModel;
using HTTPOfficial.Configuration;

namespace HTTPOfficial.Middlewares;

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
                    new ErrorResponse("Invalid auth type", "invalidAuthType"));
                return;
            }
            
            if (Enum.TryParse<AuthTypeFlags>(typeClaim.Value, out var type) && !authMetadata.AuthTypeFlags.HasFlag(type))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Unauthorized", "unauthorised"));
                return;
            }
        }

        await next(context);
    }
}
