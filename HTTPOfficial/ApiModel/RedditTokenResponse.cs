namespace HTTPOfficial.ApiModel;

public record RedditTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, string Scope);