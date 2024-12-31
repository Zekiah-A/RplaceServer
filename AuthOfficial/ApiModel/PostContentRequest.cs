namespace AuthOfficial.ApiModel;

public class PostContentRequest
{
    public IFormFile File { get; set; }

    public PostContentRequest(IFormFile file)
    {
        File = file;
    }
}