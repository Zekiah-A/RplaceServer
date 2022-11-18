using WatsonWebsocket;

namespace RplaceServer;

public record GameData
(
    int Cooldown,
    bool CaptchaEnabled,
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
    //These will change frequently through the course of the game
    public byte[] Board;
    public uint Players;
    public Dictionary<ClientMetadata, SocketClient> Clients;

    //These are persistent & saved in configs
    public int Cooldown;
    public bool CaptchaEnabled;
    public List<string> Vips { get; set; }
    public List<string> Bans;
    public int BoardWidth; //Pixels
    public int BoardHeight; //Pixels
    public int BackupFrequency; //Seconds
    public bool UseCloudflare;
    public string CanvasFolder;
    public string CanvasFile;
    
    //These are config-settable but also nullable
    public string? WebhookUrl;
    public List<int>? Palette;
};

// Palette:
// [0xff1a006d, 0xff3900be, 0xff0045ff, 0xff00a8ff, 0xff35d6ff, 0xffb8f8ff, 0xff68a300, 0xff78cc00, 0xff56ed7e, 0xff6f7500, 0xffaa9e00, 0xffc0cc00, 0xffa45024, 0xffea9036, 0xfff4e951, 0xffc13a49, 0xffff5c6a, 0xffffb394, 0xff9f1e81, 0xffc04ab4, 0xffffabe4, 0xff7f10de, 0xff8138ff, 0xffaa99ff, 0xff2f486d, 0xff26699c, 0xff70b4ff, 0xff000000, 0xff525251, 0xff908d89, 0xffd9d7d4, 0xffffffff]