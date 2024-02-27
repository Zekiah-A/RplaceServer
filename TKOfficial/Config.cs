using RplaceServer;

namespace TKOfficial;
public class Config : GameData
{
    public string? CertPath { get; init; }
    public string? KeyPath { get; init; }
    public bool LogToConsole { get; init; } = true;
    public string Origin { get; init; } = "https://rplace.live";
    public int SocketPort { get; init; } = 8080;
    public int HttpPort { get; init; } = 8081;
    public bool Ssl { get; init; } = false;
}