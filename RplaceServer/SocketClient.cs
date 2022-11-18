namespace RplaceServer;

public record SocketClient
(
    string IdIpPort,
    DateTimeOffset ConnectDate
)
{
    public int Voted { get; set; }
    public DateTimeOffset LastChat { get; set; }
    public DateTimeOffset Cooldown { get; set; }
}