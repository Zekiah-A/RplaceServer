using System.Text.Json.Serialization;

namespace HTTPOfficial;


public class Post
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Title { get; set; }
    public string Description;
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public string? ContentPath { get; set; }
    public DateTime CreationDate { get; set; }

    public Post(string username, string title, string description)
    {
        Username = username;
        Title = title;
        Description = description;
    }
}