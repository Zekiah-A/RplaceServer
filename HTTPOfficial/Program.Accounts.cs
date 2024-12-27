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
        })
        .RequireAuthorization()
        .RequireAuthentication(AuthenticationTypeFlags.Account);

        app.MapDelete("/accounts/{id:int}", async (int id, HttpContext context, DatabaseContext database) =>
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
        })
        .RequireAuthorization()
        .RequireAuthentication(AuthenticationTypeFlags.Account);
        
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
        /*var expiredAccountCodeTimer = new Timer(TimeSpan.FromMinutes(config.UnverifiedAccountExpiryMinutes))
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
                if (DateTime.Now - verification.CreationDate < TimeSpan.FromMinutes(config.UnverifiedAccountExpiryMinutes))
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
        };*/
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
        accountData.Status = AccountStatus.Deleted;
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
