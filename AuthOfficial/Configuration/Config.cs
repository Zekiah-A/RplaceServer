using AuthOfficial.DataModel;

namespace AuthOfficial.Configuration;

public class Config
{
    public const int CurrentVersion = 5;
    public int Version { get; set; }

    //  Instance & worker configurations
    public string InstanceKey { get; init; }

    public DatabaseConfiguration DatabaseConfiguration { get; init; }
    public ServerConfiguration ServerConfiguration { get; init; }
    public PostsConfiguration PostsConfiguration { get; init; }
    public AccountConfiguration AccountConfiguration { get; init; }
    public AuthConfiguration AuthConfiguration { get; init; }
    public EmailConfiguration EmailConfiguration { get; init; }
    public CensorConfiguration CensorConfiguration { get; init; }
}