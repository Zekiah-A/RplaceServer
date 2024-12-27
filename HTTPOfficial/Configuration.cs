using HTTPOfficial.DataModel;

namespace HTTPOfficial;

public class ServerConfiguration
{
    public string Origin { get; init; }
    public int Port { get; init; }
    public int SocketPort { get; init; }
    public bool UseHttps { get; init; }
    public string? CertPath { get; init; }
    public string? KeyPath { get; init; }
}

public class AuthConfiguration
{
    public string JwtSecret { get; init; }
    public string JwtIssuer { get; init; }
    public string JwtAudience { get; init; }
    public int JwtExpirationMinutes { get; init; }
    public int RefreshTokenExpirationDays { get; init; }
    public int VerificationCodeExpirationMinutes { get; init; }
    public int MaxFailedVerificationAttempts { get; init; }
    public int SignupRateLimitSeconds { get; init; }
    public int FailedVerificationAttemptResetMinutes { get; init; }
}

public class EmailConfiguration
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool UseStartTls { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
}

public class PostsConfiguration
{
    public string PostsFolder { get; init; }
    public int PostLimitSeconds { get; init; }
    public List<string> PostContentAllowedDomains { get; init; }
    public int MinBannedContentPerceptualPercent { get; set; }
    public int MaxBannedContentProcessGifFrames { get; set; }
}

public class AccountConfiguration
{
    public Dictionary<AccountTier, int> AccountTierInstanceLimits { get; init; }
    public int UnverifiedAccountExpiryMinutes = 15;
}


public class Configuration
{
    public const int CurrentVersion = 4;
    public int Version { get; set; }

    //  Instance & worker configurations
    public string InstanceKey { get; init; }
    public List<Instance> DefaultInstances { get; set; }

    public ServerConfiguration ServerConfiguration { get; init; }
    public PostsConfiguration PostsConfiguration { get; init; }
    public AccountConfiguration AccountConfiguration { get; init; }
    public AuthConfiguration AuthConfiguration { get; init; }
    public EmailConfiguration EmailConfiguration { get; init; }
}