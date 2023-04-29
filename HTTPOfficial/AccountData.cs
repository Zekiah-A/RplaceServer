namespace HTTPOfficial;

public record AccountData (
    string Username,
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
)  : AccountProfile(
    Username,
    DiscordHandle,
    TwitterHandle,
    RedditHandle,
    PixelsPlaced,
    JoinDate,
    Badges,
    UsesRedditAuthentication
);