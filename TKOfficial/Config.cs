using RplaceServer;

namespace TKOfficial;

public record Config
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
    int TimelapseLimitPeriod,
    string CertPath,
    string KeyPath,
    string Origin,
    int SocketPort,
    int HttpPort,
    bool Ssl,
    string? WebhookUrl = null,
    List<int>? Palette = null
) : GameData(Cooldown, CaptchaEnabled, CreateBackups, Vips, Bans, BoardWidth, BoardHeight, BackupFrequency, UseCloudflare, CanvasFolder, TimelapseLimitPeriod, WebhookUrl, Palette);