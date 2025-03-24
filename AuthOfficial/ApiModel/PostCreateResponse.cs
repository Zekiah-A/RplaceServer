namespace AuthOfficial.ApiModel;

public class PostCreateResponse
{        
    public int PostId { get; set; }
            
    public PostCreateResponse(int postId)
    {
        PostId = postId;
    }
}