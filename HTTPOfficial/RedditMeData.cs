namespace HTTPOfficial;

public record RedditMeResponse(string AccessToken, RedditMeData Data);
public record RedditMeData(string Id, string Name);