using HTTPOfficial.ApiModel;
using HTTPOfficial.Metadatas;

namespace HTTPOfficial.Middlewares;

public class ClaimsMiddleware
{
    private readonly RequestDelegate next;

    public ClaimsMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var claimsMetadata = endpoint?.Metadata.GetMetadata<ClaimsMetadata>();

        if (claimsMetadata != null)
        {
            foreach (var claimType in claimsMetadata.Types)
            {
                var requiredClaim = context.User.Claims.FirstOrDefault(claim => claim.Type == claimType);
                if (requiredClaim is null)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(
                        new ErrorResponse("Required claim " + claimType + " was not present in the provided access token", "missingClaims"));
                    return;
                }
            }
        }

        await next(context);
    }
}