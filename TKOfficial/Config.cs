using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RplaceServer;

namespace TKOfficial;
public class Config : GameData, IGameDataBuilder<Config>
{
    public const int LatestVersion = 1;
    public int Version { get; set; } = LatestVersion;
    public string? CertPath { get; init; } = null;
    public string? KeyPath { get; init; } = null;
    public bool LogToConsole { get; init; } = true;
    public string Origin { get; init; } = "https://rplace.live";
    public int SocketPort { get; init; } = 8080;
    public int HttpPort { get; init; } = 8081;
    public bool Ssl { get; init; } = false;

    [JsonIgnore] public override List<Regex> ChatCensorRegexes { get; set; } = [];
    [JsonIgnore] public override List<Regex> ChatAllowedDomainsRegexes { get; set; } = [];
    
    private const string CensorsFileName = "censors.txt";
    private const string AllowedDomainsFileName = "allowed_domains.txt";
    
    // Builder overrides
    public Config ConfigureModeration(Action<IModerationConfiguration> optionsAction)
    {
        var options = new ConfigureModerationOptions();
        optionsAction(options);

        UseCloudflare = options.UseCloudflare;
        ChatCooldownMs = options.ChatCooldownMs;
        CaptchaEnabled = options.CaptchaEnabled;
        CensorChatMessages = options.CensorChatMessages;

        ChatCensorRegexes = LoadRegexList(Path.Combine(StaticResourcesFolder, CensorsFileName));
        ChatAllowedDomainsRegexes = LoadRegexList(Path.Combine(StaticResourcesFolder, AllowedDomainsFileName));
        
        return this;
    }
    
    private static List<Regex> LoadRegexList(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new Regex(line))
            .ToList();
    }
}
