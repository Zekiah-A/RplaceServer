namespace HTTPOfficial;

public record RedditTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, string Scope);