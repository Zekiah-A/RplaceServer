namespace AuthOfficial.DataModel;

public abstract class AuthBase
{
    /// <summary>
    /// Private account fields
    /// </summary>
    public int Id { get; set; }
    public string SecurityStamp { get; set; }

    /// <summary>
    /// Profile & meta
    /// </summary>
    public AuthType AuthType { get; set; }
    public DateTime CreationDate { get; set; }
    // Navigation property to posts
    public List<Post> Posts { get; set; } = null!;
}