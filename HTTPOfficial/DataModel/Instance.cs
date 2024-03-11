namespace HTTPOfficial.DataModel;

public class Instance
{
    public int Id { get; set; }
    public string? VanityName { get; set; }
    public DateTime LatestSync { get; set; }
    public string ServerLocation { get; set; }
    public bool UsesHttps { get; set; }

    public int? OwnerId { get; set; }
    // Navigation property to account instance owner
    public Account? Owner { get; set; } = null!;

    // Navigation property to canvas users
    public List<CanvasUser> Users { get; set; } = [];

    public Instance() { }

    public Instance(DateTime latestSync, string serverLocation, bool usesHttps)
    {
        LatestSync = latestSync;
        ServerLocation = serverLocation;
        UsesHttps = usesHttps;
    }
}