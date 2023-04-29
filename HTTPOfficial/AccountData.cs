namespace HTTPOfficial;

public record AccountData (
    string Username,
    string Password,
    string Email,
    int AccountTier,
    List<int> Instances,
    string DiscordHandle,
    string TwitterHandle,
    string RedditHandle,
    int PixelsPlaced,
    DateTime JoinDate,
    List<Badge> Badges,
    bool UsesRedditAuthentication,
    string RedditId
)  : PublicData(
    Username,
    DiscordHandle,
    TwitterHandle,
    RedditHandle,
    PixelsPlaced,
    JoinDate,
    Badges,
    UsesRedditAuthentication
);