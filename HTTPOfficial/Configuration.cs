using HTTPOfficial.DataModel;

namespace HTTPOfficial;

public class Configuration
{
    public const int CurrentVersion = 1;

    public int Version { get; set; }
    public int SocketPort { get; init; }
    public bool UseHttps { get; init; }
    public string? CertPath { get; init; }
    public string? KeyPath { get; init; }
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; }
    public string EmailUsername { get; init; }
    public string EmailPassword { get; init; }
    public List<string> KnownWorkers { get; init; }
    public string InstanceKey { get; init; }
    public List<Instance> DefaultInstances { get; set; }
    public string RedditAuthClientId { get; init; }
    public string RedditAuthClientSecret { get; init; }
    public bool Logger { get; init; }
    public string PostsFolder { get; init; }
    public int PostLimitSeconds { get; init; }
    public int SignupLimitSeconds { get; init; }
    public int VerifyLimitSeconds { get; init; }
    public int VerifyExpiryMinutes { get; init; }
    public string Origin { get; init; }
    public int Port { get; init; }
    public Dictionary<AccountTier, int> AccountTierInstanceLimits { get; init; }
}