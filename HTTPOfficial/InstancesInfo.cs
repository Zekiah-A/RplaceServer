namespace HTTPOfficial;

public class InstancesInfo
{
    public List<int> Ids;
    public Dictionary<string, string> VanityMap;

    public InstancesInfo(List<int> ids, Dictionary<string, string> vanityMap)
    {
        Ids = ids;
        VanityMap = vanityMap;
    }

    public InstancesInfo()
    {
        Ids = new List<int>();
        VanityMap = new Dictionary<string, string>();
    }
}