using System.Text.Json.Serialization;
using AuthOfficial.Authorization;

namespace AuthOfficial.DataModel;

public class PostContent
{
    public int Id { get; set; }
    // Filesystem file name for content (usually PostId_Id.extension)
    public string ContentKey { get; set; } = null!;
    public string ContentType { get; set; } = null!; // Mime Type

    public int PostId { get; set; }
    // Navigation property to post
	[JsonIgnore]
    public Post Post { get; set; } = null!;

    public PostContent() { }

    public PostContent(string contentPath, string contentType, int postId)
    {
        ContentKey = contentPath;
        ContentType = contentType;
        PostId = postId;
    }
}