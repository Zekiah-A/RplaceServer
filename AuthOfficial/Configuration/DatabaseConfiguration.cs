using AuthOfficial.DataModel;

namespace AuthOfficial.Configuration;

public class DatabaseConfiguration
{
    public string ConnectionString { get; set; }
    public List<Instance> DefaultInstances { get; set; }
    public List<Forum> DefaultForums { get; set; }
}