namespace HTTPOfficial.DataModel;

public class Instance
{
    public int Id { get; set; }
    public string? VanityName { get; set; }
    public DateTime LatestSync { get; set; }

    public int OwnerId { get; set; }
    // Navigation property to account instance owner
    public Account Owner { get; set; } = null!;

    public Instance() { }

    public Instance(int id, DateTime latestSync)
    {
        Id = id;
        LatestSync = latestSync;
    }
}