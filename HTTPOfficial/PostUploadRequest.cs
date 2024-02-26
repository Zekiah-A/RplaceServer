namespace HTTPOfficial;

public class PostUploadRequest
{
    public string Username;
    public string Title;
    public string Description;

    public PostUploadRequest(string username, string title, string description)
    {
        Username = username;
        Title = title;
        Description = description;
    }
}