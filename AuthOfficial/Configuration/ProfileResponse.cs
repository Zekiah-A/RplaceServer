using AuthOfficial.DataModel;

namespace AuthOfficial.Configuration;

/// <summary>
/// Exists as a stopgap to prevent Account being directly cast to profile
/// (which could yield a security risk if the contents are serialised).
/// </summary>
public class ProfileResponse
{
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
    public List<AccountBadge> Badges { get; set; } = [];
}