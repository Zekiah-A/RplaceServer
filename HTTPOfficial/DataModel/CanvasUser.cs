using System.Text.Json.Serialization;

namespace HTTPOfficial.DataModel;

// Links a canvas user to an account
public class CanvasUser
{
    public int Id { get; set; }
    public int UserIntId { get; set; }

    // Navigation property to user posts
    [JsonIgnore]
    public List<Post> Posts { get; set; } = [];

    public int InstanceId { get; set; }
    // Navigation property to linked instance
    public Instance Instance { get; set; } = null!;

    public int? AccountId { get; set; }
    // Navigation property to linked account
    [JsonIgnore]
    public Account? Account { get; set; } = null!;
}