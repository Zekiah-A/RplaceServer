namespace HTTPOfficial.ApiModel;

public class PostUploadRequest
{
    public LinkageSubmission? CanvasUser { get; set; }
    public int? AccountId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public PostUploadRequest(LinkageSubmission? canvasUser, int? accountId, string title, string description)
    {
        CanvasUser = canvasUser;
        AccountId = accountId;
        Title = title;
        Description = description;
    }
}