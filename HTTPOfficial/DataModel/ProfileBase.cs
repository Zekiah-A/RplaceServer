namespace HTTPOfficial.DataModel;

/// <summary>
/// Exists as a stopgap to prevent Account being directly cast to profile
/// (which could yield a security risk if the contents are serialised).
/// </summary>
public abstract class ProfileBase
{
    // Must be unique
    public int Id { get; set; }
    // Must be unique
    public string Username { get; set; } = null!;
    
    public string? DiscordHandle { get; set; }
    public string? TwitterHandle { get; set; }
    public string? RedditHandle { get; set; }
    public int PixelsPlaced { get; set; }
    public DateTime CreationDate { get; set; }

    // Navigation property to badges
    public List<Badge> Badges { get; set; } = [];
}