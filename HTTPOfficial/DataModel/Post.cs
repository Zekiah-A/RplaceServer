namespace HTTPOfficial.DataModel;


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
    
    public int AuthorId { get; set; }
    // Navigation property to account owner
    public Account? Author { get; set; }

    public Post() { }

    public Post(string username, string title, string description)
    {
        Username = username;
        Title = title;
        Description = description;
    }
}