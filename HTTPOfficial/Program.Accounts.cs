using System.Security.Claims;
using FluentValidation;
using HTTPOfficial.ApiModel;
using HTTPOfficial.DataModel;
using HTTPOfficial.Services;


namespace HTTPOfficial;

internal static partial class Program
{
    private static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/accounts/{identifier}", async (string identifier, HttpContext context, DatabaseContext database) =>
        {
            var accountId = context.User.Claims.FindFirstAs<int>(ClaimTypes.NameIdentifier);
            var accountTier = context.User.Claims.FindFirstAs<AccountTier>("tier");

            var targetId = identifier == "me" ? accountId : int.Parse(identifier);
            if (targetId != accountId && accountTier != AccountTier.Administrator)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("You are forbidden from accessing this account's details", "accounts.forbidden"));
                return;
            }
            
            var account = await database.Accounts.FindAsync(targetId);
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
        .RequireAuthType(AuthTypeFlags.Account)
        .RequireClaims(ClaimTypes.NameIdentifier, "tier");
        
        app.MapPatch("/accounts/{identifier}/profile", async (string identifier, IValidator<ProfileUpdateRequest> validator, ProfileUpdateRequest profileUpdate, HttpContext context, CensorService censor, DatabaseContext database) =>
        {
            var accountId = context.User.Claims.FindFirstAs<int>(ClaimTypes.NameIdentifier);

            var targetId = identifier == "me" ? accountId : int.Parse(identifier);
            var account = await database.Accounts.FindAsync(targetId);
            if (account is not { Status: AccountStatus.Active })
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("Account not found", "account.notFound"));
                return;
            }

            var validationResult = await validator.ValidateAsync(profileUpdate);
            if (!validationResult.IsValid)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    "Specified profile update was invalid", 
                    "account.profile.invalidUpdate",
                    validationResult.ToDictionary()));
            }

            // Update fields if provided
            if (profileUpdate.DiscordHandle != null)
            {
                account.DiscordHandle = profileUpdate.DiscordHandle;
            }
            if (profileUpdate.TwitterHandle != null)
            {
                account.TwitterHandle = profileUpdate.TwitterHandle;
            }
            if (profileUpdate.RedditHandle != null)
            {
                account.RedditHandle = profileUpdate.RedditHandle;
            }
            if (profileUpdate.Biography != null)
            {
                account.Biography = censor.CensorBanned(profileUpdate.Biography);
            }

            // Save changes to the database
            await database.SaveChangesAsync();

            context.Response.StatusCode = StatusCodes.Status200OK;
        })
        .RequireAuthorization()
        .RequireAuthType(AuthTypeFlags.Account)
        .RequireClaims(ClaimTypes.NameIdentifier);

        app.MapDelete("/accounts/{identifier}", async (string identifier, HttpContext context, AccountService accountService, DatabaseContext database) =>
        {
            var accountId = context.User.Claims.FindFirstAs<int>(ClaimTypes.NameIdentifier);
            var accountTier = context.User.Claims.FindFirstAs<AccountTier>("tier");

            var targetId = identifier == "me" ? accountId : int.Parse(identifier);
            if (targetId != accountId && accountTier != AccountTier.Administrator)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new ErrorResponse("You are forbidden from accessing this account's details", "accounts.forbidden"));
                return;
            }

            // TODO: Research account deletion standards further
            // Fully deleting the account record can cause a lot of DB issues if all relations are not handled,
            // for now, all account data will simply be wiped (termination), but the record will remain
            var success = await accountService.TerminateAccount(targetId);
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
        .RequireAuthType(AuthTypeFlags.Account)
        .RequireClaims(ClaimTypes.NameIdentifier, "tier");
        
        app.MapGet("/profiles/{id:int}", async (int id, HttpContext context, DatabaseContext database) =>
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
    }
}
