namespace HTTPOfficial;

public record AccountData(string Username,
    string Password,
    string Email,
    int AccountTier,
    List<int> Instances,
    // Sent to client to allow them to stay signed in, server will keep refreshing their real token so this just confirms that they are the account holder, length = 42
    string RedditRefreshToken,
    bool UsesRedditAuthentication);