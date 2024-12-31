namespace AuthOfficial.ApiModel;

public class PostUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    public PostUpdateRequest(string? title, string? description)
    {
        Title = title;
        Description = description;
    }
}