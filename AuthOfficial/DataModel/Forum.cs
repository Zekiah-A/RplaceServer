using System.Text.Json.Serialization;

namespace AuthOfficial.DataModel;

public class Forum
{
    public int Id { get; set; }
    public string VanityName { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public int? AssociatedInstanceId { get; set; }
    // Navigation property to associated instance
    [JsonIgnore]
    public Instance? AssociatedInstance { get; set; }

    // Navigation property to forum posts
    public List<Post> Posts { get; set; } = [];
}