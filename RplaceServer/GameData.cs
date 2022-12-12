using WatsonWebsocket;

namespace RplaceServer;

/// <summary>
/// Dataclass to contain any data used by the server that can be modified at runtime
/// </summary>
public record GameData
(
    int Cooldown,
    bool CaptchaEnabled,
    bool CreateBackups,
    List<string> Vips,
    List<string> Bans,
    int BoardWidth,
    int BoardHeight,
    int BackupFrequency,
    bool UseCloudflare,
    string CanvasFolder,
    string? WebhookUrl = null,
    List<int>? Palette = null
)
{
    //These will be acessed & changed frequently
    public byte[] Board;
    public int PlayerCount;
    public Dictionary<ClientMetadata, SocketClient> Clients;

    //These are persistent & saved in configs
    public int Cooldown { get; set; } = Cooldown;
    public bool CaptchaEnabled { get; set; } = CaptchaEnabled;
    public List<string> Vips { get; set; } = Vips;
    public List<string> Bans { get; set; } = Bans;
    public int BoardWidth { get; set; } = BoardWidth; //Pixels
    public int BoardHeight { get; set; } = BoardHeight; //Pixels
    public int BackupFrequency { get; set; } = BackupFrequency; //Seconds
    public bool UseCloudflare { get; set; } = UseCloudflare;
    public string CanvasFolder { get; set; } = CanvasFolder;
    
    //These are config-settable, and live changeable, but not necessary (nullable)
    public string? WebhookUrl { get; set; } = WebhookUrl;
    public List<int>? Palette { get; set; } = Palette;

    public bool CreateBackups { get; set; } = CreateBackups;
}

// Default Palette:
// [0xff1a006d, 0xff3900be, 0xff0045ff, 0xff00a8ff, 0xff35d6ff, 0xffb8f8ff, 0xff68a300, 0xff78cc00, 0xff56ed7e, 0xff6f7500, 0xffaa9e00, 0xffc0cc00, 0xffa45024, 0xffea9036, 0xfff4e951, 0xffc13a49, 0xffff5c6a, 0xffffb394, 0xff9f1e81, 0xffc04ab4, 0xffffabe4, 0xff7f10de, 0xff8138ff, 0xffaa99ff, 0xff2f486d, 0xff26699c, 0xff70b4ff, 0xff000000, 0xff525251, 0xff908d89, 0xffd9d7d4, 0xffffffff]