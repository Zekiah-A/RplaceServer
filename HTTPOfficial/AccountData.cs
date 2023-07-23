using UnbloatDB.Attributes;

namespace HTTPOfficial;

public record AccountData (
    string Username, // [MustBeUnique]
    string Email, // [MustBeUnique]
    AccountTier Tier,
    List<int> Instances,
    string DiscordHandle,
    string TwitterHandle,
    string RedditHandle,
    int PixelsPlaced,
    DateTime JoinDate,
    List<Badge> Badges,
    bool UsesRedditAuthentication,
    string RedditId // [MustBeUnique]
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