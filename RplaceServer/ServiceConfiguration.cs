using System.Net.Http.Json;
using System.Text.Json;

namespace RplaceServer;

public interface IServiceConfiguration
{
    public IWebhookService? WebhookService { get; set; }
}

public interface IWebhookService
{
    public string? Url { get; set; }
    public string? ModerationUrl { get; set; }

    public void SendWebhook(WebhookBody body, HttpClient client);
    public void SendModWebhook(WebhookBody body, HttpClient client);
}

public class DiscordWebhookService : IWebhookService
{
    public string? Url { get; set; }
    public string? ModerationUrl { get; set; }
    private readonly JsonSerializerOptions defaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public void SendWebhook(WebhookBody body, HttpClient client)
    {
        client.PostAsJsonAsync(Url + "?wait=true", body, defaultJsonOptions);
    }

    public void SendModWebhook(WebhookBody body, HttpClient client)
    {
        client.PostAsJsonAsync(ModerationUrl + "?wait=true", body, defaultJsonOptions);
    }
}

public class TurnstileService
{
    public string PrivateKey;
    public string SiteKey;

    public TurnstileService(string privateKey, string siteKey)
    {
        PrivateKey = privateKey;
        SiteKey = siteKey;
    }
}

public class ConfigureServiceOptions : IServiceConfiguration
{
    public IWebhookService WebhookService { get; set; }
    public TurnstileService? TurnstileService { get; set; }
}