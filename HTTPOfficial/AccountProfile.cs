using System;
using System.Collections.Generic;

namespace HTTPOfficial;

public class AccountProfile
{

    // Must be unique
    public int Id { get; set; }
    // Must be unique
    public string Username { get; set; }
    public string DiscordHandle { get; set; }
    public string TwitterHandle { get; set; }
    public string RedditHandle { get; set; }
    public int PixelsPlaced { get; set; }
    public DateTime JoinDate { get; set; }
    public bool UsesRedditAuthentication { get; set; }

    // Navigation property to badges
    public List<AccountBadge> Badges { get; set; } = [];


    public AccountProfile(
        string username,
        string discordHandle,
        string twitterHandle,
        string redditHandle,
        int pixelsPlaced,
        DateTime joinDate,
        bool usesRedditAuthentication)
    {
        Username = username;
        DiscordHandle = discordHandle;
        TwitterHandle = twitterHandle;
        RedditHandle = redditHandle;
        PixelsPlaced = pixelsPlaced;
        JoinDate = joinDate;
        UsesRedditAuthentication = usesRedditAuthentication;
    }
}
