namespace HTTPOfficial.ApiModel;

public class ProfileUpdateRequest
{
    public string? DiscordHandle { get; set; }
    public string? TwitterHandle { get; set; }
    public string? RedditHandle { get; set; }
    public string? Biography { get; set; }
}