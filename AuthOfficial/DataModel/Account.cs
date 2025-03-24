using System.Text.Json.Serialization;
namespace AuthOfficial.DataModel;

public class Account : AuthBase
{
    /// <summary>
    /// Profile
    /// </summary>

    // Must be unique
    public string Username { get; set; } = null!;

    // Customisable
    public string? DiscordHandle { get; set; }
    public string? TwitterHandle { get; set; }
    public string? RedditHandle { get; set; }
    public string? Biography { get; set; }

    // Meta
    public int PixelsPlaced { get; set; }

    // Navigation property to badges
    public List<AccountBadge> Badges { get; set; } = [];

    /// <summary>
    /// Private account fields
    /// </summary>

    // Must be unique
    public string Email { get; set; } = null!;
    public AccountTier Tier { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    
    // Navigation property to account pending verifications
    [JsonIgnore]
    public List<AccountPendingVerification> PendingVerifications { get; set; } = [];
    // Navigation property to account instances
    [JsonIgnore]
    public List<Instance> Instances { get; set; } = [];
    // Navigation property to account linked users
    [JsonIgnore]
    public List<CanvasUser> LinkedUsers { get; set; } = [];
    // Navigation property to refresh tokens
    [JsonIgnore]
    public List<AccountRefreshToken> RefreshTokens { get; set; } = [];

    public Account() { }

    public Account(string username, string email, string securityStamp, AccountTier tier)
    {
        Username = username;
        Email = email;
        SecurityStamp = securityStamp;
        Tier = tier;
        Status = AccountStatus.Pending;
        AuthType = AuthType.Account;

        // Profile fields
        DiscordHandle = null;
        TwitterHandle = null;
        RedditHandle = null;
        Biography = null;
        PixelsPlaced = 0;
        CreationDate = DateTime.UtcNow;
    }
}
