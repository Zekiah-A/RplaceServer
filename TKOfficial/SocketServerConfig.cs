namespace TKOfficial;

public record SocketServerConfig
(
    int Width,
    int Height,
    int PaletteSize,
    int Cooldown,
    bool CaptchaEnabled,
    List<string> Vips,
    List<string> Bans,
    List<int> PaletteOverride,
    string WebhookUrl
);