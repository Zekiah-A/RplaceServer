namespace AuthOfficial.Middlewares;

public class IdentityMiddleware
{
    private readonly RequestDelegate next;

    public IdentityMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? actAs = null;
        if (context.Request.Query.TryGetValue("act-as", out var actAsQuery))
        {
            actAs = actAsQuery.ToString();
        }
        else
        {
            actAs = context.Request.Headers["X-Act-As"];
        }

        if (actAs == null)
        {
            // TODO:
        }
        
        // TODO: Finish this
        
        await next(context);
    }
}