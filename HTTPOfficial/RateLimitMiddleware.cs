using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;

namespace HTTPOfficial;

public class RateLimiterMiddleware
{
    private readonly RequestDelegate downstreamHandler;
    private readonly RateLimiter rateLimiter;

    public RateLimiterMiddleware(RequestDelegate next, TimeSpan limit)
    {
        downstreamHandler = next;
        rateLimiter = new RateLimiter(limit);
    }

    public async Task Invoke(HttpContext context, DatabaseContext database)
    {
        var ipAddress = context.Connection.RemoteIpAddress;
        if (ipAddress is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!rateLimiter.IsAuthorised(ipAddress, true))
        {
            var timeLeft = rateLimiter.GetTimeLeft(ipAddress);
            var errorResponse = new
            {
                message = $"You are being rate limited, try again in {timeLeft.TotalSeconds} seconds",
                code = "rateLimit"
            };

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            return;
        }

        await downstreamHandler(context);
    }
}
