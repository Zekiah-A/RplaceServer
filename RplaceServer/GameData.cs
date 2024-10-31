using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WatsonWebsocket;

namespace RplaceServer;

/// <summary>
/// Dataclass to contain any configurable data to be used by the server
/// </summary>
public class GameData : IGameDataBuilder<GameData>
{
    // These can be changed at runtime
    // Canvas configuration
    public uint CooldownMs { get; set; }
    public uint BoardWidth { get; set; }
    public uint BoardHeight { get; set; }
    public List<uint>? Palette { get; set; }
    public static List<uint> DefaultPalette { get; } =
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
    public int BackupFrequencyS { get; set; }
    public string StaticResourcesFolder { get; set; }
    public string SaveDataFolder { get; set;  }
    public int TimelapseLimitPeriodS { get; set; }
    public bool TimelapseEnabled { get; set; }
    public string CanvasFolder { get; set; }
    public bool CreateBackups { get; set; }

    // Moderation configuration - Required
    public bool UseCloudflare { get; set; }
    public int ChatCooldownMs { get; set; }
    public bool CaptchaEnabled { get; set; }
    public bool CensorChatMessages { get; set; }
    public virtual List<Regex> ChatCensorRegexes { get; set; }
    public virtual List<Regex> ChatAllowedDomainsRegexes { get; set; }

    // External service configuration - Optional
    // These are config-settable, and live changeable, but not necessary 
    public IWebhookService? WebhookService { get; set; }
    public TurnstileService? TurnstileService { get; set; }

    // Constructor
    public GameData() { }
    public GameData(string staticResourcesFolder, string saveDataFolder, string canvasFolder, IWebhookService? webhookService = null)
    {
        StaticResourcesFolder = staticResourcesFolder;
        SaveDataFolder = saveDataFolder;
        CanvasFolder = canvasFolder;
        WebhookService = webhookService;
    }
}
