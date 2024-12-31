using System.Text.Json.Serialization;
using AuthOfficial.Authorization;

namespace AuthOfficial.DataModel;

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public DateTime CreationDate { get; set; }
    public bool HasSensitiveContent { get; set; }
    public DateTime? LastEdited { get; set; }

    // Forum id
    public int ForumId { get; set; }
    [JsonIgnore]
    // Navigation property to post-containing forum
    public Forum Forum { get; set; }

    // Navigation property to post contents
    public List<PostContent> Contents { get; set; } = [];

    // Either canvas user or Author is used depending on if post was created
    // under a global auth server account or a linked user
    public int? CanvasUserAuthorId { get; set; }
    // Navigation property to canvas user
    [JsonIgnore]
    public CanvasUser? CanvasUserAuthor { get; set; }

    public int? AccountAuthorId { get; set; }
    // Navigation property to account owner
    [JsonIgnore]
    public Account? AccountAuthor { get; set; }

    public Post() { }

    public Post(string title, string description)
    {
        Title = title;
        Description = description;
    }
}
