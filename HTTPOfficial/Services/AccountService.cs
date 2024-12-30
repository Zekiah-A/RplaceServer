using System.Security.Cryptography;
using HTTPOfficial.DataModel;
using Microsoft.EntityFrameworkCore;

namespace HTTPOfficial.Services;

public class AccountService
{
    private readonly ILogger<AccountService> logger;
    private readonly DatabaseContext database;
    
    public AccountService(ILogger<AccountService> logger, DatabaseContext database)
    {
        this.logger = logger;
        this.database = database;
    }
    
    public async Task<bool> TerminateAccount(int accountId)
    {
        var account = await database.Accounts.FindAsync(accountId);
        if (account == null)
        {
            logger.LogError("Failed to terminate account {accountId}: account not found", accountId);
            return false;
        }
        if (account.Status == AccountStatus.Terminated)
        {
            logger.LogError("Failed to terminate account {accountId}: account was already terminated", accountId);
            return false;
        }
        
        // Purge all account data
        account.Username = "DeletedAccount#" + RandomNumberGenerator.GetInt32(0, int.MaxValue);
        account.Email = "";
        account.SecurityStamp = "";
        account.TwitterHandle = null;
        account.RedditHandle = null;
        account.DiscordHandle = null;
        account.Status = AccountStatus.Terminated;
        await database.SaveChangesAsync();
        return true;
    }

    public async Task RunPostAuthentication(Account account)
    {
        // If they have been on the site for 20+ days, we remove their noob badge
        var noobBadge = await database.Badges.FirstOrDefaultAsync(accountBadge =>
            accountBadge.OwnerId == account.Id && accountBadge.Type == BadgeType.Newbie);
        if (noobBadge is not null && DateTime.Now - account.CreationDate >= TimeSpan.FromDays(20))
        {
            database.Badges.Remove(noobBadge);
        }

        // If they have been on the site for more than a year, they get awarded a veteran badge
        var veteranBadge = await database.Badges.FirstOrDefaultAsync(accountBadge =>
            accountBadge.OwnerId == account.Id && accountBadge.Type == BadgeType.Veteran);
        if (veteranBadge is not null && DateTime.Now - account.CreationDate >= TimeSpan.FromDays(365))
        {
            database.Badges.Add(new Badge(BadgeType.Veteran, DateTime.Now));
        }

        await database.SaveChangesAsync();
    }
}