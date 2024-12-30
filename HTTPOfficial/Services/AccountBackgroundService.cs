using HTTPOfficial.Configuration;
using HTTPOfficial.DataModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HTTPOfficial.Services;

public class AccountBackgroundService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<AccountBackgroundService> logger;
    private readonly IOptionsMonitor<AccountConfiguration> config;
    private readonly AccountService accountService;

    public AccountBackgroundService(IServiceProvider serviceProvider, ILogger<AccountBackgroundService> logger, IOptionsMonitor<AccountConfiguration> config, AccountService accountService)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.config = config;
        this.accountService = accountService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AccountCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAccountsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during account cleanup.");
            }
            
            

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupExpiredAccountsAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var expirationTime = TimeSpan.FromMinutes(config.CurrentValue.UnverifiedAccountExpiryMinutes);
        var now = DateTime.UtcNow;

        var pendingVerifications = database.PendingVerifications
            .Where(verification => verification.Initial && !verification.Used && now - verification.CreationDate >= expirationTime)
            .Include(verification => verification.Account);

        await foreach (var verification in pendingVerifications.AsAsyncEnumerable())
        {
            if (verification.Account is { Status: AccountStatus.Pending })
            {
                // Account was not verified in time, it is safe to GC this account
                //DeletedUser#3275087503223
                var success = await accountService.TerminateAccount(verification.Account.Id);
                if (!success)
                {
                    logger.LogError("Failed to terminate account {AccountId}, which expired before initial verification", verification.AccountId);
                }

                database.PendingVerifications.Remove(verification);
            }

        }

        await database.SaveChangesAsync();
        logger.LogInformation("Expired accounts and pending verifications cleaned up.");
    }
}