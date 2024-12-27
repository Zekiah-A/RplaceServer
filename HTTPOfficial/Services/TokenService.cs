public class TokenService
{
    private readonly IHttpContextAccessor httpContextAccessor;
    
    public TokenService(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public void SetTokenCookies(string accessToken, string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(60)
        };

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        httpContextAccessor.HttpContext?.Response.Cookies.Append(
            "AccessToken", 
            accessToken, 
            cookieOptions);

        httpContextAccessor.HttpContext?.Response.Cookies.Append(
            "RefreshToken", 
            refreshToken, 
            refreshCookieOptions);
    }
}
