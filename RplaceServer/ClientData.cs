using RplaceServer.Types;

namespace RplaceServer;

public record ClientData
(
    string IdIpPort,
    DateTimeOffset ConnectDate
)
{
    public DateTimeOffset LastNameChange { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastChat { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset Cooldown { get; set; } = ConnectDate;

    public Permissions Permissions;
}