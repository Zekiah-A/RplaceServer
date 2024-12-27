using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using HTTPOfficial.Metadatas;
using HTTPOfficial.ApiModel;

namespace HTTPOfficial.Middlewares;

public class RequireAuthenticationMiddleware
{
    private readonly RequestDelegate next;
    private readonly IOptionsMonitor<AuthConfiguration> config;

    public RequireAuthenticationMiddleware(RequestDelegate next, IOptionsMonitor<AuthConfiguration> config)
    {
        this.next = next;
        this.config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var authMetadata = endpoint?.Metadata.GetMetadata<RequireAuthenticationMetadata>();

        if (authMetadata != null)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Unauthorized", "unauthorised"));
                return;
            }

            var principal = ValidateToken(token);
            if (principal == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
                return;
            }

            context.User = principal;
        }

        await next(context);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(config.CurrentValue.JwtSecret);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config.CurrentValue.JwtIssuer,
                ValidAudience = config.CurrentValue.JwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
