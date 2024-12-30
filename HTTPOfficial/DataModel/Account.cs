using System.Text.Json.Serialization;
namespace HTTPOfficial.DataModel;

public class Account : ProfileBase
{
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
    // Navigation property to banned contents (moderator only)
    [JsonIgnore]
    public List<BannedContent> BannedContents { get; set; } = [];
    
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

    public Profile ToProfile()
    {
        return new Profile
        {
            Id = Id,
            Username = Username,
            DiscordHandle = DiscordHandle,
            TwitterHandle = TwitterHandle,
            RedditHandle = RedditHandle,
            PixelsPlaced = PixelsPlaced,
            CreationDate = CreationDate,
            Badges = Badges.ToList()
        };
    }
}
