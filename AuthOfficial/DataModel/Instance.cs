using System.Text.Json.Serialization;

namespace AuthOfficial.DataModel;

public class Instance
{
    public int Id { get; set; }
    public string? VanityName { get; set; }
    public string ServerLocation { get; set; }
    public bool Legacy { get; set;}
    public string FileServerLocation { get; set; }
    public bool UsesHttps { get; set; }

    public int? OwnerId { get; set; }
    // Navigation property to account instance owner
    [JsonIgnore]
    public Account? Owner { get; set; } = null!;

    // Navigation property to canvas users
    [JsonIgnore]
    public List<CanvasUser> Users { get; set; } = [];

    public Instance() { }

    public Instance(string serverLocation, bool usesHttps)
    {
        ServerLocation = serverLocation;
        UsesHttps = usesHttps;
    }
}