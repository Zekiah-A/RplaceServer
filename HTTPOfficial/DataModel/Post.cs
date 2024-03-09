using System.Text.Json.Serialization;

namespace HTTPOfficial.DataModel;

public class Post
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string Title { get; set; }
    public string Description;
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public DateTime CreationDate { get; set; }
    
    [JsonIgnore]
    public string? ContentUploadKey { get; set; }
    [JsonIgnore]
    public string? ContentPath { get; set; }

    public int? AuthorId { get; set; }
    // Navigation property to account owner
    [JsonIgnore]
    public Account? Author { get; set; }


    public Post() { }

    public Post(string title, string description)
    {
        Title = title;
        Description = description;
    }
}