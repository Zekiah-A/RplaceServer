using RplaceServer;

namespace TKOfficial;

public record Config
(
    uint Cooldown,
    int ChatCooldown,
    bool CaptchaEnabled,
    bool CreateBackups,
    uint BoardWidth,
    uint BoardHeight,
    int BackupFrequency,
    bool UseCloudflare,
    string CanvasFolder,
    int TimelapseLimitPeriod,
    bool CensorChatMessages,
    string StaticResourcesFolder,
    string SaveDataFolder,
    bool SaveChatMessageHistory,
    bool LogToConsole,
    string CertPath,
    string KeyPath,
    string Origin,
    int SocketPort,
    int HttpPort,
    bool Ssl,
    string WebhookUrl,
    string ModWebhookUrl,
    List<uint>? Palette = null
) : GameData(Cooldown, ChatCooldown, CaptchaEnabled, CreateBackups, BoardWidth, BoardHeight, BackupFrequency, UseCloudflare, 
    CanvasFolder, TimelapseLimitPeriod, CensorChatMessages, StaticResourcesFolder, SaveDataFolder, SaveChatMessageHistory,
    WebhookUrl, ModWebhookUrl, Palette);