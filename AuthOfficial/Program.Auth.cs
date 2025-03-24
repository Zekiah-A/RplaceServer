using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using AuthOfficial.ApiModel;
using AuthOfficial.Configuration;
using AuthOfficial.DataModel;
using AuthOfficial.Services;
using Microsoft.Extensions.Options;

namespace AuthOfficial;


internal static partial class Program
{
    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime LastAttempt)> failedAttempts = new();
    private static readonly ConcurrentDictionary<string, DateTime> signupAttempts = new();

    private static List<string> trustedEmailDomains = null!;
    private static EmailAddressAttribute emailAttributes = null!;

    private static void MapAuthEndpoints(this WebApplication app)
    {
        // Email and trusted domains
        trustedEmailDomains = ReadTxtListFile("trusted_domains.txt");
        emailAttributes = new EmailAddressAttribute();

        app.MapPost("/auth/signup", async (EmailUsernameRequest request, HttpContext context, TokenService tokenService, EmailService email, IOptionsSnapshot<AuthConfiguration> config, DatabaseContext database) =>
        {
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
                if (DateTime.UtcNow.Subtract(lastAttempt).TotalSeconds < config.Value.SignupRateLimitSeconds)
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
            var securityStamp = TokenService.GenerateSecurityStamp();
            var account = new Account(
                request.Username,
                request.Email,
                securityStamp,
                AccountTier.Bronze);
            await database.Accounts.AddAsync(account);
            await database.SaveChangesAsync();

            // Generate and send verification code
            var verificationCode = await CreateVerificationCodeAsync(account.Id, config, database, true);
            await email.SendVerificationEmailAsync(account.Email, verificationCode);
            
            // Generate initial JWT (limited capabilities until email is verified)
            var (accessToken, refreshToken) = tokenService.GenerateTokens(account, emailVerified: false);
            await tokenService.StoreRefreshTokenAsync(account, refreshToken);
            tokenService.SetTokenCookies(accessToken, refreshToken);

            // Send response success
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(
                account.Id,
                account.Username,
                account.Email,
                emailVerified: false
            ));
        });

        app.MapPost("/auth/login", async (EmailUsernameRequest request, HttpContext context, TokenService tokenService, EmailService email, IOptionsSnapshot<AuthConfiguration> config, DatabaseContext database) =>
        {
            var account = await database.Accounts
                .FirstOrDefaultAsync(account => account.Email == request.Email && account.Username == request.Username);

            if (account is not { Status: AccountStatus.Active })
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Invalid credentials", "auth.login.invalidCredentials"));
                return;
            }

            // Generate and send verification code
            var verificationCode = await CreateVerificationCodeAsync(account.Id, config, database);
            await email.SendLoginVerificationEmailAsync(account.Email, account.Username, verificationCode);

            // Generate initial JWT (limited capabilities until email is verified)
            var (accessToken, refreshToken) = tokenService.GenerateTokens(account, emailVerified: false);
            await tokenService.StoreRefreshTokenAsync(account, refreshToken);
            tokenService.SetTokenCookies(accessToken, refreshToken);

            // Send response sxuccess
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(
                account.Id,
                account.Username,
                account.Email,
                emailVerified: false
            ));
        });

        app.MapPost("/auth/verify", async (VerifyCodeRequest request, HttpContext context, TokenService tokenService, AccountService accountService, IOptionsSnapshot<AuthConfiguration> config, DatabaseContext database) =>
        {
            // Check for too many failed attempts
            var attemptKey = $"verify_{request.AccountId}";
            if (failedAttempts.TryGetValue(attemptKey, out var attempts))
            {
                if (attempts.Attempts >= config.Value.MaxFailedVerificationAttempts)
                {
                    if (DateTime.UtcNow.Subtract(attempts.LastAttempt).TotalMinutes < config.Value.FailedVerificationAttemptResetMinutes)
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
            if (account == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Account not found", "auth.verify.accountNotFound"));
                return;
            }
            await accountService.RunPostAuthentication(account);

            // Mark account as verified & active
            if (verification.Initial && account.Status == AccountStatus.Pending)
            {
                account.Status = AccountStatus.Active;
            }

            // Mark verification as used
            verification.Used = true;
            await database.SaveChangesAsync();

            // Generate full JWT
            var (accessToken, refreshToken) = tokenService.GenerateTokens(account, true);
            await tokenService.StoreRefreshTokenAsync(account, refreshToken);
            tokenService.SetTokenCookies(accessToken, refreshToken);

            // Send response success
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(
                account.Id,
                account.Username,
                account.Email,
                emailVerified: true
            ));
        });

        app.MapGet("/auth/logout", async (TokenService tokenService, HttpContext context) =>
        {
            var accountId = context.User.Claims.FindFirstAs<int>(JwtRegisteredClaimNames.Sub);

            tokenService.ClearTokenCookies();
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LogoutResponse(
                accountId
            ));
        })
        .RequireAuthorization()
        .RequireAuthType(AuthType.Account);

        // Authenticate a client as a Canvas User
        app.MapPost("/auth/link", async (LinkageSubmission request, HttpContext context, TokenService tokenService, IOptionsSnapshot<AuthConfiguration> config, DatabaseContext database) =>
        {
            // Attempt to verify the Canvas User with the given link key 
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
                var securityStamp = TokenService.GenerateSecurityStamp();
                var newCanvasUser = new CanvasUser(request.InstanceId, (int) userIntId, securityStamp);
                await database.CanvasUsers.AddAsync(newCanvasUser);
                await database.SaveChangesAsync();

                // Use canvas user from newly created record
                canvasUser = newCanvasUser;
            }

            // Generate full JWT
            var (accessToken, refreshToken) = tokenService.GenerateTokens(canvasUser);
            await tokenService.StoreRefreshTokenAsync(canvasUser, refreshToken);
            tokenService.SetTokenCookies(accessToken, refreshToken);

            // Send response success
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new AuthLinkResponse(
                canvasUser.Id,
                accessToken,
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

    // Uses the linkage API to prove that with a given link key, a client owns an instance user account
    private static async Task<int?> VerifyCanvasUserIntId(int instanceId, string linkKey, DatabaseContext database)
    {
        var logPrefix = $"Could not verify canvas user with instanceId {instanceId}.";

        // Lookup instance
        var instance = await database.Instances.FindAsync(instanceId);
        if (instance is null)
        {
            logger.LogInformation("{logPrefix} Instance not found", logPrefix);
            return null;
        }

        // Verify with instance that they do own given link key
        var instanceUri = (instance.UsesHttps ? "https://" : "http://") + instance.ServerLocation;
        var linkVerifyUri = $"{instanceUri}/link/{linkKey}";
        var linkResponse = await httpClient.GetAsync(linkVerifyUri);
        if (linkResponse.IsSuccessStatusCode)
        {
            var linkData = await linkResponse.Content.ReadFromJsonAsync<LinkData>(defaultJsonOptions);
            if (linkData is null)
            {
                logger.LogInformation("{logPrefix} JSON data returned by server was invalid", logPrefix);
                return null;
            }

            // The user's int ID according to the canvas server
            return linkData.IntId;
        }

        logger.LogInformation("{logPrefix} Server denied linkage request ({statusCode} {content})",
            logPrefix, linkResponse.StatusCode, await linkResponse.Content.ReadAsStringAsync());
        return null;
    }

    private static async Task<string> CreateVerificationCodeAsync(int accountId, IOptionsSnapshot<AuthConfiguration> config, DatabaseContext database, bool initial = false)
    {
        var code = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString();
        
        var verification = new AccountPendingVerification
        {
            AccountId = accountId,
            Code = code,
            CreationDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddMinutes(config.Value.VerificationCodeExpirationMinutes),
            Used = false,
            Initial = initial
        };

        await database.PendingVerifications.AddAsync(verification);
        await database.SaveChangesAsync();

        return code;
    }
}
