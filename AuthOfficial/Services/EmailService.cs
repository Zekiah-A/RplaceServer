using AuthOfficial.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace AuthOfficial.Services;

public class EmailService
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IOptionsMonitor<EmailConfiguration> config;
    private readonly ILogger logger;

    // Track failed email attempts to prevent spam
    private static readonly Dictionary<string, (int Attempts, DateTime LastAttempt)> failedAttempts  = new();
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutPeriod = TimeSpan.FromHours(1);

    public EmailService(IHttpContextAccessor httpContextAccessor, IOptionsMonitor<EmailConfiguration> config, ILogger logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.config = config;
        this.logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string code)
    {
        var subject = "Verify Your Account";
        var body = GenerateEmailTemplate("AccountVerification", new Dictionary<string, string>
        {
            { "Code", code },
            { "ExpirationMinutes", "15" },
            { "WebsiteUrl", config.CurrentValue.WebsiteUrl }
        });

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendLoginVerificationEmailAsync(string toEmail, string username, string code)
    {
        // Get client information for security context
        var clientIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
        var userAgent = httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown Device";

        
        var subject = "Login Verification Code";
        var body = GenerateEmailTemplate("LoginVerification", new Dictionary<string, string>
        {
            { "Username", username },
            { "Code", code },
            { "ExpirationMinutes", "15" },
            { "IPAddress", clientIp },
            { "UserAgent", userAgent },
            { "WebsiteUrl", config.CurrentValue.WebsiteUrl }
        });

        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        // Check for too many failed attempts
        if (!CanSendEmail(toEmail))
        {
            logger.LogWarning("Email sending blocked due to too many recent failures: {email}", toEmail);
            throw new RateLimitExceededException("Too many failed email attempts. Please try again later.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.CurrentValue.FromName, config.CurrentValue.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };

        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient
            {
                Timeout = config.CurrentValue.TimeoutSeconds * 1000
            };

            await client.ConnectAsync(
                config.CurrentValue.SmtpHost,
                config.CurrentValue.SmtpPort,
                config.CurrentValue.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

            await client.AuthenticateAsync(config.CurrentValue.Username, config.CurrentValue.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            // Reset failed attempts on success
            if (failedAttempts.ContainsKey(toEmail))
            {
                failedAttempts.Remove(toEmail);
            }
        }
        catch (Exception ex)
        {
            // Track failed attempt
            TrackFailedAttempt(toEmail);

            logger.LogError(ex, "Failed to send email to {email}: {error}", toEmail, ex.Message);
            throw new EmailSendException("Failed to send email", ex);
        }
    }

    private bool CanSendEmail(string email)
    {
        if (failedAttempts.TryGetValue(email, out var attempts))
        {
            if (attempts.Attempts >= MaxFailedAttempts)
            {
                return DateTime.UtcNow.Subtract(attempts.LastAttempt) > LockoutPeriod;
            }
        }
        return true;
    }

    private void TrackFailedAttempt(string email)
    {
        if (!failedAttempts.ContainsKey(email))
        {
            failedAttempts[email] = (1, DateTime.UtcNow);
        }
        else
        {
            var (attempts, _) = failedAttempts[email];
            failedAttempts[email] = (attempts + 1, DateTime.UtcNow);
        }
    }

    private string GenerateEmailTemplate(string templateName, Dictionary<string, string> parameters)
    {
        // Load the base template
        var template = GetEmailTemplate(templateName);
        
        // Replace all parameters in the template
        foreach (var param in parameters)
        {
            template = template.Replace($"{{{param.Key}}}", param.Value);
        }

        return $"""
            <div style="background-color: #f0f0f0;font-family: 'IBM Plex Sans', sans-serif;border-radius: 8px 8px 0px 0px;overflow: clip;height: 100%;display: flex;flex-direction: column;">
                <div role="heading" style="background: orangered;color: white;padding: 8px;box-shadow: 0px 2px 4px #0000002b;display: flex;align-items: center;column-gap: 8px;">
                    <img src="https://raw.githubusercontent.com/rslashplace2/rslashplace2.github.io/main/images/rplace.png" style="background: white;border-radius: 8px;" height="56">
                    <h1 style="margin: 0px;">rplace.live: {templateName.ToSentenceCase()}</h1>
                </div>
                <div role="main" style="margin: 8px;flex-grow: 1;">
                    {template}
                </div>
                <div role="contentinfo" style="opacity: 0.6;display: flex;flex-direction: row;padding: 16px;column-gap: 16px;">
                    <span>Email sent at {DateTime.Now}</span>
                    <hr>
                    <span>Feel free to reply</span>
                    <hr>
                    <span>Contact <a href="mailto:admin@rplace.live" style="text-decoration: none;">admin@rplace.live</a></span>
                </div>
            </div>
        """;
    }

    private string GetEmailTemplate(string templateName) => templateName switch
    {
        "AccountVerification" => """
                <h1>👋 Hello there!</h1>
                <p>Someone used your email to register a new <a href="https://rplace.live" style="text-decoration: none;">rplace.live</a> account.</p>
                <p>If that's you, then cool, your code is:</p>
                <h1 style="background-color: #13131314;display: inline;padding: 4px;border-radius: 4px;"> {Code} </h1>
                <p>Otherwise, you can ignore this email, we'll try not to message you again ❤️.</p>
                <p>This code will expire in {ExpirationMinutes} minutes.</p>
            """,
        "LoginVerification" => """
                <h2>Login Verification Required</h2>
                <p>👋 Hello {Username},</p>
                <p>A login attempt was made from:</p>
                <ul style="background-color: #f5f5f5; padding: 15px;">
                    <li>IP Address: {IPAddress}</li>
                    <li>Device: {UserAgent}</li>
                </ul>
                <p>If that's you, then cool, to complete the login, use this verification code:</p>
                <div style="background-color: #f5f5f5; padding: 15px; margin: 20px 0; text-align: center; font-size: 24px; font-weight: bold;">
                    {Code}
                </div>
                <p>Otherwise, you can ignore this email, and your account will remain secure ✅.</p>
                <p>This code will expire in {ExpirationMinutes} minutes.</p>
            """,
        _ => throw new ArgumentException($"Unknown email template: {templateName}")
    };
}

public class EmailSendException : Exception
{
    public EmailSendException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

public class RateLimitExceededException : Exception
{
    public RateLimitExceededException(string message) 
        : base(message)
    {
    }
}