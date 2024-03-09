using System.Text.Json.Serialization;
namespace HTTPOfficial.DataModel;

public class Account : AccountProfile
{
    // Must be unique
    public string Email { get; set; }
    public string Token { get; set; }

    public AccountTier Tier { get; set; }
    public string? RedditId { get; set; }

    // Navigation property to account instances
    [JsonIgnore]
    public List<Instance> Instances { get; set; } = [];
    // Navigation property to account posts
    [JsonIgnore]
    public List<Post> Posts { get; set; } = [];
    // Navigation property to account linked users
    public List<LinkedUser> LinkedUsers { get; set; } = [];

    public Account() { }

    public Account(string username, string email, string token, AccountTier tier, DateTime joinDate)
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
        JoinDate = joinDate;
        UsesRedditAuthentication = false;
    }
}
