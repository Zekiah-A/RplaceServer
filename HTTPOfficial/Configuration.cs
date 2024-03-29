using HTTPOfficial.DataModel;

namespace HTTPOfficial;

public record struct Configuration(
    int Port,
    bool UseHttps,
    string CertPath,
    string KeyPath,
    string SmtpHost,
    int SmtpPort,
    string EmailUsername,
    string EmailPassword,
    List<string> KnownWorkers,
    string InstanceKey,
    string Canvas1ServerLocation,
    bool Canvas1UsesHttps,
    string RedditAuthClientId,
    string RedditAuthClientSecret,
    bool Logger,
    string PostsFolder,
    int PostLimitSeconds,
    int SignupLimitSeconds,
    string Origin,
    int HttpPort,
    Dictionary<AccountTier, int> AccountTierInstanceLimits);