using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MimeKit;
using Timer = System.Timers.Timer;

namespace HTTPOfficial;

internal static partial class Program
{
    [GeneratedRegex(@"^\w{4,16}$")]
    private static partial Regex UsernameRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9._-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,6}$")]
    private static partial Regex EmailRegex();
    
    // /accounts/create
    [GeneratedRegex(@"^\/accounts\/create\/*$")]
    private static partial Regex AccountCreateEndpointRegex();
    
    // /accounts/{id:int}/verify
    [GeneratedRegex(@"^\/accounts\/\d+\/verify\/*$")]
    private static partial Regex AccountVerifyEndpointRegex();

    // /accounts/{id:int}/delete
    [GeneratedRegex(@"^\/accounts\/\d+\/delete\/*$")]
    private static partial Regex AccountDeleteEndpointRegex();

    // /accounts/{id:int}
    [GeneratedRegex(@"^\/accounts\/\d+\/*$")]
    private static partial Regex AccountEndpointRegex();

    [GeneratedRegex(@"^\/account\/login\/token")]
    private static partial Regex AccountLoginTokenRegex();

    private static void ConfigureAccountEndpoints()
    {
        // Email and trusted domains
        var emailAttributes = new EmailAddressAttribute();
        var trustedEmailDomains = ReadTxtListFile("trusted_domains.txt");

        app.MapPost("/accounts/create", async ([FromBody] EmailUsernameRequest request, HttpContext context, DatabaseContext database) =>
        {
            if (request.Username.Length is < 4 or > 32 || !UsernameRegex().IsMatch(request.Username))
            {
                return Results.BadRequest(new ErrorResponse("Invalid username", "account.create.invalidUsername"));
            }

            if (request.Email.Length is > 320 or < 4 || !emailAttributes.IsValid(request.Email)
                || !EmailRegex().IsMatch(request.Email)
                || trustedEmailDomains.BinarySearch(request.Email.Split('@').Last()) < 0)
            {
                return Results.BadRequest(new ErrorResponse("Invalid email", "account.create.invalidEmail"));
            }

            var emailExists = await database.Accounts.AnyAsync(account => account.Email == request.Email);
            if (emailExists)
            {
                return Results.BadRequest(new ErrorResponse("Account with specified email already exists",
                    "account.create.emailExists"));
            }

            var usernameExists = await database.Accounts.AnyAsync(account => account.Username == request.Username);
            if (usernameExists)
            {
                return Results.BadRequest(new ErrorResponse("Account with specified username already exists",
                    "account.create.usernameExists"));
            }

            var authCode = RandomNumberGenerator.GetInt32(100_000, 999_999);
            var newToken = RandomNumberGenerator.GetHexString(64);
            var accountData = new Account(request.Username, request.Email, newToken, AccountTier.Free, DateTime.Now);
            await database.Accounts.AddAsync(accountData);
            await database.SaveChangesAsync();
            await RunPostAuthentication(accountData, database);
            
            var verification = new AccountPendingVerification(accountData.Id, authCode.ToString(), DateTime.Now)
            {
                Initial = true
            };
            database.PendingVerifications.Add(verification);
            await database.SaveChangesAsync();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.EmailUsername, config.EmailUsername));
            message.To.Add(new MailboxAddress(request.Username, request.Email));
            message.Subject = "rplace.live Account Creation Code";
            message.Body = new TextPart("html")
            {
                Text = 
                    $"""
                     <div role="heading" style="background-color: #f0f0f0;font-family: 'IBM Plex Sans', sans-serif;border-radius: 8px 8px 0px 0px;overflow: clip;height: 100%;display: flex;flex-direction: column;">
                         <div style="background: orangered;color: white;padding: 8px;box-shadow: 0px 2px 4px #0000002b;display: flex;align-items: center;column-gap: 8px;">
                             <img src="https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/images/rplace.png" style="background: white;border-radius: 8px;" height="56">
                             <h1 style="margin: 0px;">rplace.live: Account Creation Code</h1>
                         </div>
                         <div role="main" style="margin: 8px;flex-grow: 1;">
                             <h1>👋 Hello there!</h1>
                             <p>Someone used your email to register a new <a href="https://rplace.live" style="text-decoration: none;">rplace.live</a> account.</p>
                             <p>If that's you, then cool, your code is:</p>
                             <h1 style="background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;"> {authCode} </h1>
                             <p>Otherwise, you can ignore this email, we'll try not to message you again ❤️.</p>
                         </div>
                         <div role="contentinfo" style="opacity: 0.6;display: flex;flex-direction: row;padding: 16px;column-gap: 16px;">
                             <span>Email sent at {DateTime.Now}</span>
                             <hr>
                             <span>Feel free to reply</span>
                             <hr>
                             <span>Contact <a href="mailto:admin@rplace.live" style="text-decoration: none;">admin@rplace.live</a></span>
                         </div>
                     </div>
                     """
            };
            
            try
            {
                using var smtpClient = new SmtpClient();
                await smtpClient.ConnectAsync(config.SmtpHost, config.SmtpPort,
                    SecureSocketOptions.StartTlsWhenAvailable);
                await smtpClient.AuthenticateAsync(config.EmailUsername, config.EmailPassword);
                await smtpClient.SendAsync(message);
                await smtpClient.DisconnectAsync(true);
            }
            catch (Exception exception)
            {
                logger.LogError("Could not send email message: {exception}", exception);
                var errorString = JsonSerializer.Serialize(new ErrorResponse("Failed to send email message",
                    "account.create.emailFailed"));
                return Results.Problem(errorString);
            }

            return Results.Ok(new LoginDetailsResponse(accountData.Id, accountData.Token));
        }).UseMiddleware<RateLimiterMiddleware>(app, AccountCreateEndpointRegex, TimeSpan.FromSeconds(config.SignupLimitSeconds));

        app.MapPost("/accounts/{id:int}/verify", async (int id, [FromBody] AccountVerifyRequest request, DatabaseContext database) =>
        {
            var verification = await database.PendingVerifications.FirstOrDefaultAsync(
                verification => verification.Code == request.Code && verification.AccountId == id);
            if (verification is null)
            {
                return Results.NotFound(new ErrorResponse("Invalid code provided", "account.verify.invalidCode"));
            }

            database.PendingVerifications.Remove(verification);
            await database.SaveChangesAsync();

            var accountData = await database.Accounts.FindAsync(verification.AccountId);
            if (accountData is null)
            {
                return Results.NotFound(new ErrorResponse("Account for given code not found",
                    "account.verify.notFound"));
            }

            await RunPostAuthentication(accountData, database);
            return Results.Ok(new LoginDetailsResponse(accountData.Id, accountData.Token));
        }).UseMiddleware<TokenAuthMiddleware>(app, AccountVerifyEndpointRegex);

        app.MapPost("/accounts/login", async ([FromBody] EmailUsernameRequest request, DatabaseContext database) =>
        {
            var accountData = await database.Accounts.FirstOrDefaultAsync(account =>
                account.Email == request.Email && account.Username == request.Username);
            if (accountData is null || accountData.Terminated)
            {
                return Results.NotFound(new ErrorResponse("Account with specified details does not exist",
                    "account.login.notFound"));
            }

            await RunPostAuthentication(accountData, database);
            return Results.Ok(new LoginDetailsResponse(accountData.Id, accountData.Token));
        });
        
        app.MapPost("/accounts/login/token", async ([FromBody] AccountTokenRequest? tokenRequest, HttpContext context, DatabaseContext database) =>
        {
            var token = tokenRequest?.Token ?? context.Items["Token"]?.ToString();
            if (token is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("No token provided in header or auth body", "accounts.login.noToken"));
                return;
            }

            var account = await database.Accounts.FirstOrDefaultAsync(account => account.Token == token);
            if (account is null || account.Terminated)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Specified token was invalid",
                    "accounts.login.invalidToken"));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(account.Id, account.Token));
        }).UseMiddleware<TokenAuthMiddleware>(app, AccountLoginTokenRegex);
        
        app.MapPost("/accounts/login/reddit", () =>
        {
            throw new NotImplementedException();
        });

        app.MapGet("/accounts/{id:int}", async (int id, HttpContext context, DatabaseContext database) =>
        {
            var token = context.Items["Token"]?.ToString();
            if (token is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("No token provided in header or auth body", "accounts.login.noToken"));
                return;
            }

            var account = await database.Accounts.FindAsync(id);
            if (account is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Specified account does not exist", "account.notFound"));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(account);
        }).UseMiddleware<TokenAuthMiddleware>(app, AccountEndpointRegex);

        app.MapDelete("/accounts/{id:int}/delete", async (int id, HttpContext context, DatabaseContext database) =>
        {
            var token = context.Items["Token"]?.ToString();
            if (token is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("No token provided in header or auth body", "accounts.login.noToken"));
                return;
            }

            // TODO: Research account deletion standards further
            // Fully deleting the account record can cause a lot of DB issues if all relations are not handled,
            // for now, all account data will simply be wiped (termination), but the record will remain
            var success = await TerminateAccountData(id, database);
            if (!success)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Specified account does not exist", "account.delete.notFound"));
                return;
            }

            await database.SaveChangesAsync();
            context.Response.StatusCode = StatusCodes.Status200OK;
        }).UseMiddleware<TokenAuthMiddleware>(app, AccountDeleteEndpointRegex);
        
        app.MapGet("/profiles/{id:int}", async (int id, DatabaseContext database) =>
        {
            var account = await database.Accounts.FindAsync(id);
            if (account is null)
            {
                return Results.NotFound(new ErrorResponse("Specified profile does not exist",
                    "account.profile.notFound"));
            }

            var profile = account.ToProfile();
            return Results.Ok(profile);
        });
        
        // Delete accounts that have not verified within the valid timespan
        var expiredAccountCodeTimer = new Timer(TimeSpan.FromMinutes(config.VerifyExpiryMinutes))
        {
            Enabled = true,
            AutoReset = true
        };
        expiredAccountCodeTimer.Elapsed += async (_, _) =>
        {
            using var scope = app.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            var pendingVerifications = database.PendingVerifications.AsAsyncEnumerable();
            await foreach (var verification in pendingVerifications)
            {
                if (DateTime.Now - verification.CreationDate < TimeSpan.FromMinutes(config.VerifyExpiryMinutes))
                {
                    continue;
                }
                
                if (verification.Initial)
                {
                    // Delete the account associated as well (assume it's a stranded a throwaway that can't be logged into again)
                    var success = await TerminateAccountData(verification.AccountId, database);
                    if (!success)
                    {
                        logger.LogError("Failed to terminate account ${AccountId}, which expired before initial verification", verification.AccountId);
                    }
                }
                
                database.PendingVerifications.Remove(verification);
            }
            await database.SaveChangesAsync();
        };

    }

    private static async Task<bool> TerminateAccountData(int accountId, DatabaseContext database)
    {
        var accountData = await database.Accounts.FindAsync(accountId);
        if (accountData is null)
        {
            return false;
        }
        accountData.Username = "Deleted Account";
        accountData.Email = "";
        accountData.Token = "";
        accountData.TwitterHandle = null;
        accountData.TwitterHandle = null;
        accountData.TwitterHandle = null;
        accountData.Terminated = true;
        return true;
    }

    // ReSharper disable once SuggestBaseTypeForParameter
    private static async Task RunPostAuthentication(Account accountData, DatabaseContext database)
    {
        // If they have been on the site for 20+ days, we remove their noob badge
        var noobBadge = await database.Badges.FirstOrDefaultAsync(accountBadge =>
            accountBadge.OwnerId == accountData.Id && accountBadge.Type == BadgeType.Newbie);
        if (noobBadge is not null && DateTime.Now - accountData.CreationDate >= TimeSpan.FromDays(20))
        {
            database.Badges.Remove(noobBadge);
        }

        // If they have been on the site for more than a year, they get awarded a veteran badge
        var veteranBadge = await database.Badges.FirstOrDefaultAsync(accountBadge =>
            accountBadge.OwnerId == accountData.Id && accountBadge.Type == BadgeType.Veteran);
        if (veteranBadge is not null && DateTime.Now - accountData.CreationDate >= TimeSpan.FromDays(365))
        {
            database.Badges.Add(new Badge(BadgeType.Veteran, DateTime.Now));
        }

        await database.SaveChangesAsync();
    }
}
