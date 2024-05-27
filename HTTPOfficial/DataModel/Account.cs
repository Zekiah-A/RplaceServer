using System.Text.Json.Serialization;
namespace HTTPOfficial.DataModel;

public class Account : ProfileBase
{
    // Must be unique
    public string Email { get; set; } = null!;
    // Must be unique
    public string Token { get; set; } = null!;
    public AccountTier Tier { get; set; }
    public bool Terminated { get; set; } = false;
    
    public int? RedditAuthId { get; set; }
    // Navigation property to account reddit auth properties
    [JsonIgnore]
    public AccountRedditAuth? RedditAuth { get; set; } 
    
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

    public Account() { }

    public Account(string username, string email, string token, AccountTier tier, DateTime creationDate)
    {
        Email = email;
        Tier = tier;
        Token = token;

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
