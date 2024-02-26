using System;
using System.Collections.Generic;

namespace HTTPOfficial;

public class AccountData : AccountProfile
{
    // Must be unique
    public string Email { get; set; }

    public AccountTier Tier { get; set; }
    public string? RedditId { get; set; }

    // Navigation property to account instances
    public List<int> Instances { get; set; } = [];

    public AccountData(string username, string email, AccountTier tier, AccountProfile profile, string redditId)
        : base(
            username,
            profile.DiscordHandle,
            profile.TwitterHandle,
            profile.RedditHandle,
            profile.PixelsPlaced,
            profile.JoinDate,
            profile.UsesRedditAuthentication)
    {
        Email = email;
        Tier = tier;

        // Profile fields
        DiscordHandle = profile.DiscordHandle;
        TwitterHandle = profile.TwitterHandle;
        RedditHandle = profile.RedditHandle;
        PixelsPlaced = profile.PixelsPlaced;
        JoinDate = profile.JoinDate;
        UsesRedditAuthentication = profile.UsesRedditAuthentication;
        RedditId = redditId;
    }
}
