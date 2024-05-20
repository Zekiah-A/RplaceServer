using HTTPOfficial.ApiModel;
using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial;

public class TokenAuthMiddleware
{
    private readonly RequestDelegate downstreamHandler;

    public TokenAuthMiddleware(RequestDelegate next)
    {
        downstreamHandler = next;
    }

    public async Task Invoke(HttpContext context, DatabaseContext database)
    {
        // Explicit header takes precedent over cookie, cookie used for tokenLogin to disocurage local saving of
        // token in localStorage. Use header auth when client has already logged in and received their token for the session
        var token = context.Request.Headers.Authorization.FirstOrDefault()
            ?? context.Request.Cookies["Authorization"]
            ?? context.Request.Query["authorization"].FirstOrDefault();

        if (token is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("Invalid token provided in auth header", "invalidToken"));
            return;
        }

        context.Items["Token"] = token;
        await downstreamHandler(context);
    }
}