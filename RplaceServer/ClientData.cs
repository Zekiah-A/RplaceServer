using RplaceServer.Types;

namespace RplaceServer;

public record ClientData
(
    string IdIpPort,
    DateTimeOffset ConnectDate
)
{
    public DateTimeOffset LastChat { get; set; } = ConnectDate;
    public DateTimeOffset Cooldown { get; set; } = ConnectDate;

    public Permissions Permissions;
}