using System.Text.Json.Serialization;
using AuthOfficial.Authorization;

namespace AuthOfficial.DataModel;

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public DateTime CreationDate { get; set; }
    public bool HasSensitiveContent { get; set; }
    public DateTime? LastEdited { get; set; }

    // Forum Id
    public int ForumId { get; set; }
    [JsonIgnore]
    // Navigation property to post-containing forum
    public Forum Forum { get; set; }

    // Navigation property to post contents
    public List<PostContent> Contents { get; set; } = [];
    
    // Discriminator
    //public PostType Type { get; set; }

    public int AuthorId { get; set; }
    // Navigation property to post author
    public AuthBase Author { get; set; } = null!;

    public Post() { }

    public Post(string title, string description)
    {
        Title = title;
        Description = description;
    }
}