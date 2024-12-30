namespace AuthOfficial.Configuration;

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