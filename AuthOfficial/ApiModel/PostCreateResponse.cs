namespace AuthOfficial.DataModel;

public class PostCreateResponse
{        
    public int PostId { get; set; }
            
    public PostCreateResponse(int postId)
    {
        PostId = postId;
    }
}