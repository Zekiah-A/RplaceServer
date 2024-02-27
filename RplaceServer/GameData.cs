using System.Text.Json.Serialization;
using WatsonWebsocket;

namespace RplaceServer;

/// <summary>
/// Dataclass to contain any data used by the server that can be modified at runtime
/// </summary>
public class GameData : ICanvasConfiguration, IStorageConfiguration, IModerationConfiguration, IServiceConfiguration
{
    // These will be accessed & changed frequently, but should not be saved in
    // main configs, and are instead managed by the server instance itself
    [JsonIgnore] public int PlayerCount = 0;
    [JsonIgnore] public byte[] Board = Array.Empty<byte>();
    [JsonIgnore] public Dictionary<ClientMetadata, ClientData> Clients = new();
    [JsonIgnore] public Dictionary<string, string> PendingCaptchas = new();
    [JsonIgnore] public Dictionary<string, long> Bans = new();
    [JsonIgnore] public Dictionary<string, long> Mutes = new();
    [JsonIgnore] public List<string> VipKeys = [];

    // These are persistent & saved in configs + can be changed at runtime
    // Canvas configuration
    public uint CooldownMs { get; set; }
    public uint BoardWidth { get; set; }
    public uint BoardHeight { get; set; }
    public List<uint>? Palette { get; set; }
    public static IReadOnlyList<uint> DefaultPalette { get; } =
    [
        0xff1a006d, 0xff3900be, 0xff0045ff, 0xff00a8ff,
        0xff35d6ff, 0xffb8f8ff, 0xff68a300, 0xff78cc00,
        0xff56ed7e, 0xff6f7500, 0xffaa9e00, 0xffc0cc00,
        0xffa45024, 0xffea9036, 0xfff4e951, 0xffc13a49,
        0xffff5c6a, 0xffffb394, 0xff9f1e81, 0xffc04ab4,
        0xffffabe4, 0xff7f10de, 0xff8138ff, 0xffaa99ff,
        0xff2f486d, 0xff26699c, 0xff70b4ff, 0xff000000,
        0xff525251, 0xff908d89, 0xffd9d7d4, 0xffffffff
    ];
    
    // Storage configuration
    public int BackupFrequencyMs { get; set; }
    public string StaticResourcesFolder { get; set; }
    public string SaveDataFolder { get; set;  }
    public bool UseDatabase { get; set; }
    public int TimelapseLimitPeriodS { get; set; }
    public string CanvasFolder { get; set; }
    public bool CreateBackups { get; set; }

    // Moderation configuration - Required
    public bool UseCloudflare { get; set; }
    public int ChatCooldownMs { get; set; }
    public bool CaptchaEnabled { get; set; }
    public bool CensorChatMessages { get; set; }
    
    // Service configuration - Optional
    // These are config-settable, and live changeable, but not necessary 
    public IWebhookService? WebhookService { get; set; }

    // Constructor
    protected GameData()
    {
        
    }
    public GameData(string staticResourcesFolder, string saveDataFolder, string canvasFolder, IWebhookService? webhookService = null)
    {
        StaticResourcesFolder = staticResourcesFolder;
        SaveDataFolder = saveDataFolder;
        CanvasFolder = canvasFolder;
        WebhookService = webhookService;
    }
    // Builder
    public static GameData CreateGameData()
    {
        return new GameData();
    }

    public GameData ConfigureCanvas()
    {
        return ConfigureCanvas(_ => {});
    }

    public GameData ConfigureCanvas(Action<ConfigureCanvasOptions> optionsAction)
    {
        var options = new ConfigureCanvasOptions();
        optionsAction(options);
        CooldownMs = options.CooldownMs;
        BoardWidth = options.BoardWidth;
        BoardHeight = options.BoardHeight;
        Palette = options.Palette;
        return this;
    }
    
    public GameData ConfigureStorage()
    {
        return ConfigureStorage(_ => {});
    }

    public GameData ConfigureStorage(Action<ConfigureStorageOptions> optionsAction)
    {
        var options = new ConfigureStorageOptions();
        optionsAction(options);
        BackupFrequencyMs = options.BackupFrequencyMs;
        StaticResourcesFolder = options.StaticResourcesFolder;
        SaveDataFolder = options.SaveDataFolder;
        CanvasFolder = options.CanvasFolder;
        UseDatabase = options.UseDatabase;
        TimelapseLimitPeriodS = options.TimelapseLimitPeriodS;
        CreateBackups = options.CreateBackups;
        return this;
    }
    
    public GameData ConfigureModeration()
    {
        return ConfigureModeration(_ => {});
    }
    
    public GameData ConfigureModeration(Action<ConfigureModerationOptions> optionsAction)
    {
        var options = new ConfigureModerationOptions();
        optionsAction(options);
        UseCloudflare = options.UseCloudflare;
        ChatCooldownMs = options.ChatCooldownMs;
        CaptchaEnabled = options.CaptchaEnabled;
        CensorChatMessages = options.CensorChatMessages;
        return this;
    }
    
    public GameData ConfigureServices()
    {
        return ConfigureServices(_ => {});
    }

    public GameData ConfigureServices(Action<ConfigureServiceOptions> optionsAction)
    {
        var options = new ConfigureServiceOptions();
        optionsAction(options);
        WebhookService = options.WebhookService;
        return this;
    }
}
