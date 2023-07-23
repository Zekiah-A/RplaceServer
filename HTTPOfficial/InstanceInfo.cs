namespace HTTPOfficial;

public class InstanceInfo
{
    public int Id;
    public DateTime LatestSync;

    public InstanceInfo(int id, DateTime latestSync)
    {
        Id = id;
        LatestSync = latestSync;
    }
}