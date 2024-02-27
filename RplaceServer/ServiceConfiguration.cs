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

public class ConfigureServiceOptions : IServiceConfiguration
{
    public string? WebhookUrl
    {
        set => WebhookService.Url = value;
    }

    public string? ModWebhookUrl
    {
        set => WebhookService.ModerationUrl = value;
    }
    
    public IWebhookService WebhookService { get; set; } = new DiscordWebhookService();
}