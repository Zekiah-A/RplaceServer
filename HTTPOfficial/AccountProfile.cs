namespace HTTPOfficial;

public record AccountProfile(
    string Username,
    string DiscordId,
    string TwitterHandle,
    string RedditHandle,
    int PixelsPlaced,
    DateTime JoinDate,
    List<Badge> Badges,
    bool UsesRedditAuthentication)
{
    public string Username { get; set; } = Username;
    public string DiscordId { get; set; } = DiscordId;
    public string TwitterHandle { get; set; } = TwitterHandle;
    public string RedditHandle { get; set; } = RedditHandle;
    public int PixelsPlaced { get; set; } = PixelsPlaced;
    public List<Badge> Badges { get; set; } = Badges;
    public bool UsesRedditAuthentication { get; set; } = UsesRedditAuthentication;
}