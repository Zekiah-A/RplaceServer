namespace RplaceServer;

public record SocketClient
(
    string IdIpPort
)
{
    public int Voted { get; set; }
    public int LastChat { get; set; }
    public int Cooldown { get; set; }
}