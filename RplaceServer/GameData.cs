using System.Text.Json.Serialization;
using WatsonWebsocket;

namespace RplaceServer;

/// <summary>
/// Dataclass to contain any data used by the server that can be modified at runtime
/// </summary>
public record GameData
(
    uint Cooldown, // Milliseconds
    int ChatCooldown, // Milliseconds
    bool CaptchaEnabled,
    bool CreateBackups,
    uint BoardWidth,
    uint BoardHeight,
    int BackupFrequency, // Milliseconds
    bool UseCloudflare,
    string CanvasFolder,
    int TimelapseLimitPeriod, // Milliseconds
    bool CensorChatMessages,
    string StaticResourcesFolder,
    string SaveDataFolder,
    bool SaveChatMessageHistory,
    string WebhookUrl,
    string ModWebhookUrl,
    List<uint>? Palette
)
{
    // These will be accessed & changed frequently, but are not saved in main configs, 
    // and are instead managed by the server instance itself.
    public int PlayerCount = 0;
    public byte[] Board = Array.Empty<byte>();
    public Dictionary<ClientMetadata, ClientData> Clients = new();
    public Dictionary<string, string> PendingCaptchas = new();
    public Dictionary<string, long> Bans = new();
    public Dictionary<string, long> Mutes = new();
    public List<string> VipKeys = new();

    // These are persistent & saved in configs + can be changed at runtime
    // Milliseconds
    public uint Cooldown { get; set; } = Cooldown;
    // Milliseconds
    public int ChatCooldown { get; set; } = ChatCooldown;
    public bool CaptchaEnabled { get; set; } = CaptchaEnabled;
    // Pixels
    public uint BoardWidth { get; set; } = BoardWidth;
    // Pixels
    public uint BoardHeight { get; set; } = BoardHeight;
    // Milliseconds
    public int BackupFrequency { get; set; } = BackupFrequency;
    // Will perform cloudflare cf-clearance headers checks if present to allow only cloudflare validated clients
    public bool UseCloudflare { get; set; } = UseCloudflare;
    // By default will use regexes provided in SocketServer.cs, however these can be changed
    public bool CensorChatMessages { get; set; } = CensorChatMessages;
    // Directory where resources, such as Pages and Captcha Generation assets will be stored,
    // multiple instances can technically share a resources directory as their content is static.
    public string StaticResourcesFolder { get; set; } = StaticResourcesFolder;
    // Will contain save data produced by this instance, excluding canvas data, such as log records, bans, mutes and other
    // such instance data.
    public string SaveDataFolder { get; set;  } = SaveDataFolder;
    // Will dictate whether chat message history is saved and sent to clients upon connection. Chat messages are saved in
    // a LiteDB SQL-like database within the instance save data directory for easy queries. 
    public bool SaveChatMessageHistory { get; set; } = SaveChatMessageHistory;
    
    
    // These are config-settable, and live changeable, but not necessary (nullable)
    public string WebhookUrl { get; set; } = WebhookUrl;
    public string ModWebhookUrl { get; set; } = ModWebhookUrl;
    public List<uint>? Palette { get; set; } = Palette;
    public bool CreateBackups { get; set; } = CreateBackups;
}

// Default Palette:
// [0xff1a006d, 0xff3900be, 0xff0045ff, 0xff00a8ff, 0xff35d6ff, 0xffb8f8ff, 0xff68a300, 0xff78cc00, 0xff56ed7e, 0xff6f7500,
// 0xffaa9e00, 0xffc0cc00, 0xffa45024, 0xffea9036, 0xfff4e951, 0xffc13a49, 0xffff5c6a, 0xffffb394, 0xff9f1e81, 0xffc04ab4,
// 0xffffabe4, 0xff7f10de, 0xff8138ff, 0xffaa99ff, 0xff2f486d, 0xff26699c, 0xff70b4ff, 0xff000000, 0xff525251, 0xff908d89,
// 0xffd9d7d4, 0xffffffff]