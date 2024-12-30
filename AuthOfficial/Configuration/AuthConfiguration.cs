namespace AuthOfficial.Configuration;

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