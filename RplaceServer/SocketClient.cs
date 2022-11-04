namespace RplaceServer;

public record SocketClient
(
    string IdIpPort
)
{
    public int Voted { get; set; }
    public DateTimeOffset LastChat { get; set; }
    public DateTimeOffset Cooldown { get; set; }
}