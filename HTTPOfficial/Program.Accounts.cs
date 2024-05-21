using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace HTTPOfficial;

internal static partial class Program
{
    private static void ConfigureAccountEndpoints()
    {
        var random = new Random();
        var signupLimiter = new RateLimiter(TimeSpan.FromSeconds(config.SignupLimitSeconds));

        // Email and trusted domains
        var emailAttributes = new EmailAddressAttribute();
        var trustedEmailDomains = ReadTxtListFile("trusted_domains.txt");

        app.MapPost("/accounts/create", async ([FromBody] EmailUsernameRequest request, HttpContext context, DatabaseContext database) =>
        {
            if (context.Connection.RemoteIpAddress is not { } ipAddress || !signupLimiter.IsAuthorised(ipAddress))
            {
                return Results.Unauthorized();
            }

            if (request.Username.Length is < 4 or > 32 || !UsernameRegex().IsMatch(request.Username))
            {
                return Results.BadRequest(new ErrorResponse("Invalid username", "account.create.invalidUsername"));
            }

            if (request.Email.Length is > 320 or < 4 || !emailAttributes.IsValid(request.Email) ||
                !EmailRegex().IsMatch(request.Email)
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
            var accountData = new Account(request.Username, request.Email, newToken, AccountTier.Free, DateTime.Now)
            {
                VerificationCode = authCode.ToString()
            };
            await database.Accounts.AddAsync(accountData);
            await database.SaveChangesAsync();
            await RunPostAuthentication(accountData, database);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.EmailUsername, config.EmailUsername));
            message.To.Add(new MailboxAddress(request.Username, request.Email));
            message.Subject = "rplace.live Account Creation Code";
            message.Body = new TextPart("html")
            {
                Text = $"""
                        <div style="background-color: #f0f0f0;font-family: 'IBM Plex Sans', sans-serif;border-radius: 8px 8px 0px 0px;overflow: clip;">
                            <h1 style="background: orangered;color: white;padding: 8px;box-shadow: 0px 2px 4px #0000002b;">👋 Hello there!</h1>
                            <div style="margin: 8px;">
                                <p>Someone used your email to register a new <a href="https://rplace.live" style="text-decoration: none;">rplace.live</a> account.</p>
                                <p>If that's you, then cool, your code is:</p>
                                <h1 style="background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;"> {authCode} </h1>
                                <p>Otherwise, you can ignore this email, who cares anyway??</p>
                                <img src="https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/images/rplace.png">
                                <p style="opacity: 0.6;">Email sent at {DateTime.Now} | Feel free to reply | Contact
                                <a href="mailto:admin@rplace.live" style="text-decoration: none;">admin@rplace.live</a></p>
                            <div>
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

            return Results.Ok();
        });

        app.MapPost("/accounts/create/verify", async ([FromBody] AccountVerifyRequest request, HttpContext context, DatabaseContext database) =>
        {
            var accountData = await database.Accounts.FirstOrDefaultAsync(
                account => account.VerificationCode == request.VerificationCode);
            if (accountData is null)
            {
                return Results.NotFound(new ErrorResponse("Invalid code provided", "account.verify.invalidCode"));
            }

            accountData.VerificationCode = null;
            await database.SaveChangesAsync();

            await RunPostAuthentication(accountData, database);
            return Results.Ok(new LoginDetailsResponse(accountData.Id, accountData.Token));
        });
        app.UseWhen
        (
            context => context.Request.Path.StartsWithSegments("/account/login/verify"),
            appBuilder => appBuilder.UseMiddleware<TokenAuthMiddleware>()
        );

        app.MapPost("/accounts/login", async ([FromBody] EmailUsernameRequest request, DatabaseContext database) =>
        {
            var accountData = await database.Accounts.FirstOrDefaultAsync(account =>
                account.Email == request.Email && account.Username == request.Username);
            if (accountData is null)
            {
                return Results.NotFound(new ErrorResponse("Account with specified details does not exist",
                    "account.login.notFound"));
            }

            await RunPostAuthentication(accountData, database);
            return Results.Ok(new LoginDetailsResponse(accountData.Id, accountData.Token));
        });

        app.MapPost("/accounts/login/verify", async ([FromBody] AccountVerifyRequest request, HttpContext context, DatabaseContext database) =>
        {
            throw new NotImplementedException();

            /*var address = context.Connection.RemoteIpAddress;
            if (address is null)
            {
                return Results.Unauthorized();
            }

            if (await database.Accounts.FindAsync(id) is not { } account)
            {
                return Results.NotFound(new ErrorResponse("Specified account does not exist", "accounts.verify.notFound"));
            }

            if (!emailAuthCompletions.TryGetValue(id, out var completion))
            {
                return Results.NotFound(new ErrorResponse("Specified account has no pending verification code", "accounts.verify.noCompletion"));
            }

            if (completion.Address.ToString() != address.ToString() || completion.AuthCode != request.VerificationCode)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new LoginDetailsResponse(account.Id, account.Token));*/
        });
        app.UseWhen
        (
            context => context.Request.Path.StartsWithSegments("/account/login/verify"),
            appBuilder => appBuilder.UseMiddleware<TokenAuthMiddleware>()
        );

        app.MapPost("/accounts/login/token", async ([FromBody] string? token, HttpContext context, DatabaseContext database) =>
        {
            token ??= context.Items["Token"]?.ToString();
            if (token is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("No token provided in header or auth body", "accounts.login.noToken"));
                return;
            }

            var account = await database.Accounts.FirstOrDefaultAsync(account => account.Token == token);
            if (account is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Specified token was invalid",
                    "accounts.login.invalidToken"));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new LoginDetailsResponse(account.Id, account.Token));
        });

        app.MapPost("/accounts/login/reddit", () =>
        {
            throw new NotImplementedException();
        });

        app.MapGet("/accounts/{id:int}", async (int id, DatabaseContext database) =>
        {
            var account = await database.Accounts.FindAsync(id);
            if (account is null)
            {
                return Results.NotFound(new ErrorResponse("Specified account does not exist", "account.notFound"));
            }

            return Results.Ok(account);
        });
        app.UseWhen
        (
            context => AccountEndpointRegex().IsMatch(context.Request.Path),
            appBuilder => appBuilder.UseMiddleware<TokenAuthMiddleware>()
        );

        app.MapDelete("/accounts/{id:int}/delete", async (int id, DatabaseContext database) =>
        {
            var profile = await database.Accounts.FindAsync(id);
            if (profile is null)
            {
                return Results.NotFound(
                    new ErrorResponse("Speficied account does not exist", "account.delete.notFound"));
            }

            database.Accounts.Remove(profile);
            await database.SaveChangesAsync();
            return Results.Ok();
        });
        app.UseWhen
        (
            context => AccountDeleteEndpointRegex().IsMatch(context.Request.Path),
            appBuilder => appBuilder.UseMiddleware<TokenAuthMiddleware>()
        );

        app.MapGet("/profiles/{id:int}", async (int id, DatabaseContext database) =>
        {
            var profile = (AccountProfile?)await database.Accounts.FindAsync(id);
            if (profile is null)
            {
                return Results.NotFound(new ErrorResponse("Specified profile does not exist",
                    "account.profile.notFound"));
            }

            return Results.Ok(profile);
        });
    }

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