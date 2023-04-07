using RplaceServer;

namespace TKOfficial;

public record Config
(
    int Cooldown,
    int ChatCooldown,
    bool CaptchaEnabled,
    bool CreateBackups,
    List<string> Vips,
    List<string> Bans,
    List<string> Muted,
    int BoardWidth,
    int BoardHeight,
    int BackupFrequency,
    bool UseCloudflare,
    string CanvasFolder,
    string PostsFolder,
    int PostLimitPeriod,
    int TimelapseLimitPeriod,
    bool CensorChatMessages,
    int ChatHistoryLength,
    
    bool LogToConsole,
    string CertPath,
    string KeyPath,
    string Origin,
    int SocketPort,
    int HttpPort,
    bool Ssl,
    
    string? WebhookUrl = null,
    List<uint>? Palette = null
) : GameData(Cooldown, ChatCooldown, CaptchaEnabled, CreateBackups, Vips, Bans, Muted, BoardWidth, BoardHeight, BackupFrequency, UseCloudflare, 
    CanvasFolder, PostsFolder, PostLimitPeriod, TimelapseLimitPeriod, CensorChatMessages, ChatHistoryLength, WebhookUrl, Palette);