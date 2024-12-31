namespace AuthOfficial.ApiModel;

public class PostUploadRequest
{
    public int ForumId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public PostUploadRequest(int forumId, string title, string description)
    {
        ForumId = forumId;
        Title = title;
        Description = description;
    }
}