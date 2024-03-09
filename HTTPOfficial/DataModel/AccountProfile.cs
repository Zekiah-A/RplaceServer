namespace HTTPOfficial.DataModel;

public class AccountProfile
{

    // Must be unique
    public int Id { get; set; }
    // Must be unique
    public string Username { get; set; }
    public string? DiscordHandle { get; set; }
    public string? TwitterHandle { get; set; }
    public string? RedditHandle { get; set; }
    public int PixelsPlaced { get; set; }
    public DateTime JoinDate { get; set; }
    public bool UsesRedditAuthentication { get; set; }

    // Navigation property to badges
    public List<Badge> Badges { get; set; } = [];
}
