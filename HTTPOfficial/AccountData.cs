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

    public AccountData(string username, string email, AccountTier tier, DateTime joinDate)
    {
        Email = email;
        Tier = tier;

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
