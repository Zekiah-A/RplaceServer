namespace HTTPOfficial;

public record RedditTokenResponse(string AccessToken, string TokenType, string ExpiresIn, string Scope, string RefreshToken);