using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using HTTPOfficial.Services;
using Microsoft.AspNetCore.Identity.Data;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace HTTPOfficial;


internal static partial class Program
{
    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime LastAttempt)> failedAttempts = new();
    private static readonly ConcurrentDictionary<string, DateTime> signupAttempts = new();

    private static List<string> trustedEmailDomains = null!;
    private static EmailAddressAttribute emailAttributes = null!;

    private static void ConfigureAuthEndpoints()
    {
        // Email and trusted domains
        trustedEmailDomains = ReadTxtListFile("trusted_domains.txt");
        emailAttributes = new EmailAddressAttribute();

        app.MapPost("/auth/signup", async (EmailUsernameRequest request, HttpContext context, EmailService email, AuthConfiguration config, DatabaseContext database) => {
            // Rate limiting check
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            if (ipAddress is null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Failed to resolve IP address", "auth.signup.ipAddress"));
                return;
            }
            if (signupAttempts.TryGetValue(ipAddress, out var lastAttempt))
            {
                if (DateTime.UtcNow.Subtract(lastAttempt).TotalSeconds < config.SignupRateLimitSeconds)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsJsonAsync(
                        new ErrorResponse("Too many signup attempts. Please try again later.", "auth.signup.rateLimit"));
                    return;
                }
            }
            signupAttempts[ipAddress] = DateTime.UtcNow;

            // Validate input
            if (!IsValidUsername(request.Username))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid username format", "auth.signup.invalidUsername"));
                return;
            }
            if (!IsValidEmail(request.Email))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid email format", "auth.signup.invalidEmail"));
                return;
            }

            // Check for existing accounts
            if (await database.Accounts.AnyAsync(account => account.Email == request.Email || account.Username == request.Username))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Account already exists", "auth.signup.accountExists"));
                return;
            }

            // Create account with secure defaults
            var account = new Account
            {
                Username = request.Username,
                Email = request.Email,
                CreationDate = DateTime.UtcNow,
                Status = AccountStatus.Pending,
                SecurityStamp = GenerateSecurityStamp()
            };

            // Generate verification code
            var verificationCode = await CreateVerificationCodeAsync(account.Id, config, database);
            
            // Store account
            await database.Accounts.AddAsync(account);
            await database.SaveChangesAsync();

            // Send verification email
            await email.SendVerificationEmailAsync(account.Email, account.Username, verificationCode);

            // Generate initial JWT (limited capabilities until email is verified)
            var (token, refreshToken) = GenerateTokens(account, emailVerified: false, config);
            
            // Store refresh token
            await StoreRefreshTokenAsync(account, refreshToken, config, database);

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(
                account.Id,
                token,
                refreshToken,
                account.Username,
                account.Email
            ));
        });

        app.MapPost("/auth/login", async (EmailUsernameRequest request, HttpContext context, EmailService email, AuthConfiguration config, DatabaseContext database) => {
            var account = await database.Accounts
                .FirstOrDefaultAsync(a => a.Email == request.Email && a.Username == request.Username);

            if (account == null || account.Status != AccountStatus.Active)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid credentials", "auth.login.invalidCredentials"));
                return;
            }

            // Generate and send verification code
            var verificationCode = await CreateVerificationCodeAsync(account.Id, config, database);
            await email.SendLoginVerificationEmailAsync(account.Email, account.Username, verificationCode, context);

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(
                account.Id,
                token: null,
                refreshToken: null,
                account.Username,
                account.Email,
                emailVerified: false
            ));
            return;
        });

        app.MapPost("/auth/verify", async (VerifyCodeRequest request, HttpContext context, AuthConfiguration config, DatabaseContext database) => {
            // Check for too many failed attempts
            var attemptKey = $"verify_{request.AccountId}";
            if (failedAttempts.TryGetValue(attemptKey, out var attempts))
            {
                if (attempts.Attempts >= config.MaxFailedVerificationAttempts)
                {
                    if (DateTime.UtcNow.Subtract(attempts.LastAttempt).TotalMinutes < config.FailedVerificationAttemptResetMinutes)
                    {
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.Response.WriteAsJsonAsync(
                            new ErrorResponse("Too many failed attempts. Please try again later.", "auth.verify.rateLimit"));
                        return;
                    }

                    failedAttempts.Remove(attemptKey, out var _);
                }
            }

            var verification = await database.PendingVerifications
                .FirstOrDefaultAsync(verification => 
                    verification.AccountId == request.AccountId && 
                    verification.Code == request.Code &&
                    verification.ExpirationDate > DateTime.UtcNow &&
                    !verification.Used);

            if (verification == null)
            {
                // Track failed attempt
                if (!failedAttempts.ContainsKey(attemptKey))
                {
                    failedAttempts[attemptKey] = (1, DateTime.UtcNow);
                }
                else
                {
                    failedAttempts[attemptKey] = (attempts.Attempts + 1, DateTime.UtcNow);
                }
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid or expired verification code", "auth.verify.invalidCode"));
                return;
            }

            var account = await database.Accounts.FindAsync(request.AccountId);
            if (account == null || account.Status != AccountStatus.Active)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Account not found or inactive", "auth.verify.accountNotFound"));
                return;
            }

            // Mark verification as used
            verification.Used = true;
            await database.SaveChangesAsync();

            // Generate tokens
            var (token, refreshToken) = GenerateTokens(account, true, config);
            
            // Store refresh token
            await StoreRefreshTokenAsync(account, refreshToken, config, database);

            // Send the LoginDetailsResponse as json and return 200
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(
                account.Id,
                token,
                refreshToken,
                account.Username,
                account.Email,
                emailVerified: true
            ));
            return;
        });

        // Authenticate a client as a Canvas User
        app.MapPost("/auth/link", async (LinkageSubmission request, HttpContext context, AuthConfiguration config, DatabaseContext database) =>
        {
            // We need to perform
            var userIntId = await VerifyCanvasUserIntId(request.InstanceId, request.LinkKey, database);
            if (userIntId is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid or expired link key", "auth.link.invalidKey"));
                return;
            }

            // Lookup if there is a CanvasUser with this intId, if not we can create it
            var canvasUser = await database.CanvasUsers.FirstOrDefaultAsync(user => user.UserIntId == userIntId);
            if (canvasUser is null)
            {
                var newCanvasUser = new CanvasUser()
                {
                    InstanceId = request.InstanceId,
                    UserIntId = (int) userIntId
                };
                await database.CanvasUsers.AddAsync(newCanvasUser);
                await database.SaveChangesAsync();

                // Use canvas user from newly created record
                canvasUser = newCanvasUser;
            }

            // Generate JWT token
            var (token, refreshToken) = GenerateTokens(canvasUser, config);
            await StoreRefreshTokenAsync(canvasUser, refreshToken, config, database);

            // Send the LoginDetailsResponse as json and return 200
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new AuthLinkResponse(
                canvasUser.Id,
                token,
                refreshToken,
                canvasUser.UserIntId,
                canvasUser.InstanceId
            ));
        });
    }

    private static bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 4 || username.Length > 16)
        {
            return false;
        }

        username = username.ToLower();

        var validChars = new Regex(@"^[a-z0-9._]+$");
        var consecutivePeriods = new Regex(@"\.\.");

        return validChars.IsMatch(username) && !consecutivePeriods.IsMatch(username);
    }

    private static bool IsValidEmail(string email)
    {
        var emailAttribute = new EmailAddressAttribute();
        return emailAttribute.IsValid(email) && trustedEmailDomains.Contains(email.Split('@')[1]);
    }

    private static (string token, string refreshToken) GenerateTokens(CanvasUser user, AuthConfiguration config)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("type", AuthType.CanvasUser.ToString()),
            new("userIntId", user.UserIntId.ToString()),
            new("instanceId", user.InstanceId.ToString()),
            new("securityStamp", user.SecurityStamp)
        };
        var token = GenerateSecurityToken(claims, config);
        var refreshToken = GenerateRefreshToken();
        return (new JwtSecurityTokenHandler().WriteToken(token), refreshToken);
    }

    private static (string token, string refreshToken) GenerateTokens(Account account, bool emailVerified, AuthConfiguration config)
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
        var token = GenerateSecurityToken(claims, config);
        var refreshToken = GenerateRefreshToken();
        return (new JwtSecurityTokenHandler().WriteToken(token), refreshToken);
    }

    private static JwtSecurityToken GenerateSecurityToken(List<Claim> claims, AuthConfiguration config)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

        var token = new JwtSecurityToken(
            issuer: config.JwtIssuer,
            audience: config.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(config.JwtExpirationMinutes),
            signingCredentials: creds);

        return token;
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static string GenerateSecurityStamp()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static async Task<string> CreateVerificationCodeAsync(int accountId, AuthConfiguration config, DatabaseContext database)
    {
        var code = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString();
        
        var verification = new AccountPendingVerification
        {
            AccountId = accountId,
            Code = code,
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddMinutes(config.VerificationCodeExpirationMinutes),
            Used = false
        };

        await database.PendingVerifications.AddAsync(verification);
        await database.SaveChangesAsync();

        return code;
    }

    private static async Task StoreRefreshTokenAsync(CanvasUser canvasUser, string refreshToken, AuthConfiguration config, DatabaseContext database)
    {
        var canvasUserToken = new CanvasUserRefreshToken
        {
            CanvasUserId = canvasUser.Id,
            Token = refreshToken,
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(config.RefreshTokenExpirationDays)
        };

        await database.CanvasUserRefreshTokens.AddAsync(canvasUserToken);
        await database.SaveChangesAsync();
    }

    private static async Task StoreRefreshTokenAsync(Account account, string refreshToken, AuthConfiguration config, DatabaseContext database)
    {
        var accountToken = new AccountRefreshToken
        {
            AccountId = account.Id,
            Token = refreshToken,
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddDays(config.RefreshTokenExpirationDays)
        };

        await database.AccountRefreshTokens.AddAsync(accountToken);
        await database.SaveChangesAsync();
    }
}
