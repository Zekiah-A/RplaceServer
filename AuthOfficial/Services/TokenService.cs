using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthOfficial.Services;

public class TokenService
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IOptionsMonitor<AuthConfiguration> config;
    private readonly DatabaseContext database;
    
    public TokenService(IHttpContextAccessor httpContextAccessor, IOptionsMonitor<AuthConfiguration> config, DatabaseContext database)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.config = config;
        this.database = database;
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

    public void ClearTokenCookies()
    {
        var expiredCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(-1)
        };

        httpContextAccessor.HttpContext?.Response.Cookies.Append(
            "AccessToken", 
            string.Empty, 
            expiredCookieOptions);

        httpContextAccessor.HttpContext?.Response.Cookies.Append(
            "RefreshToken", 
            string.Empty, 
            expiredCookieOptions);
    }

    public (string token, string refreshToken) GenerateTokens(CanvasUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("type", AuthType.CanvasUser.ToString()),
            new("userIntId", user.UserIntId.ToString()),
            new("instanceId", user.InstanceId.ToString()),
            new("securityStamp", user.SecurityStamp)
        };
        var token = GenerateSecurityToken(claims);
        var refreshToken = GenerateRefreshToken();
        return (new JwtSecurityTokenHandler().WriteToken(token), refreshToken);
    }

    public (string token, string refreshToken) GenerateTokens(Account account, bool emailVerified)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new(JwtRegisteredClaimNames.Name, account.Username),
            new("type", AuthType.Account.ToString()),
            new("tier", account.Tier.ToString()),
            new("emailVerified", emailVerified.ToString()),
            new("securityStamp", account.SecurityStamp)
        };
        var token = GenerateSecurityToken(claims);
        var refreshToken = GenerateRefreshToken();
        return (new JwtSecurityTokenHandler().WriteToken(token), refreshToken);
    }

    public JwtSecurityToken GenerateSecurityToken(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.CurrentValue.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var token = new JwtSecurityToken(
            issuer: config.CurrentValue.JwtIssuer,
            audience: config.CurrentValue.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(config.CurrentValue.JwtExpirationMinutes),
            signingCredentials: creds);

        return token;
    }

    public static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public async Task StoreRefreshTokenAsync(CanvasUser canvasUser, string refreshToken)
    {
        var canvasUserToken = new CanvasUserRefreshToken
        {
            CanvasUserId = canvasUser.Id,
            Token = refreshToken,
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(config.CurrentValue.RefreshTokenExpirationDays)
        };

        await database.CanvasUserRefreshTokens.AddAsync(canvasUserToken);
        await database.SaveChangesAsync();
    }

    public async Task StoreRefreshTokenAsync(Account account, string refreshToken)
    {
        var accountToken = new AccountRefreshToken
        {
            AccountId = account.Id,
            Token = refreshToken,
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(config.CurrentValue.RefreshTokenExpirationDays)
        };

        await database.AccountRefreshTokens.AddAsync(accountToken);
        await database.SaveChangesAsync();
    }

    public static string GenerateSecurityStamp()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}