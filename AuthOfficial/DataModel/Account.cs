using System.Text.Json.Serialization;
namespace AuthOfficial.DataModel;

public class Account
{
    // Profile

    // Must be unique
    public int Id { get; set; }
    // Must be unique
    public string Username { get; set; } = null!;

    // Customisable
    public string? DiscordHandle { get; set; }
    public string? TwitterHandle { get; set; }
    public string? RedditHandle { get; set; }
    public string? Biography { get; set; }

    // Meta
    public DateTime CreationDate { get; set; }
    public int PixelsPlaced { get; set; }

    // Navigation property to badges
    public List<Badge> Badges { get; set; } = [];


    // Private account fields

    // Must be unique
    public string Email { get; set; } = null!;
    public AccountTier Tier { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Pending;
    public string SecurityStamp { get; set; } = null!;
    
    //public int? RedditAuthId { get; set; }
    // Navigation property to account reddit auth properties
    //[JsonIgnore]
    //public AccountRedditAuth? RedditAuth { get; set; } 
    
    // Navigation property to account pending verifications
    [JsonIgnore]
    public List<AccountPendingVerification> PendingVerifications { get; set; } = [];

    // Navigation property to account instances
    [JsonIgnore]
    public List<Instance> Instances { get; set; } = [];
    // Navigation property to account posts
    [JsonIgnore]
    public List<Post> Posts { get; set; } = [];    
    // Navigation property to account linked users
    [JsonIgnore]
    public List<CanvasUser> LinkedUsers { get; set; } = [];
    // Navigation property to refresh tokens
    [JsonIgnore]
    public List<AccountRefreshToken> RefreshTokens { get; set; } = new();


    public Account() { }

    public Account(string username, string email, AccountTier tier, DateTime creationDate)
    {
        Email = email;
        Tier = tier;

        // Profile fields
        Username = username;
        DiscordHandle = null;
        TwitterHandle = null;
        RedditHandle = null;
        PixelsPlaced = 0;
        CreationDate = creationDate;
    }
}
