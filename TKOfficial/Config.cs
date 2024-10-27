using RplaceServer;

namespace TKOfficial;
public class Config : GameData
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
}