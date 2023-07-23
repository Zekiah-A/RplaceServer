using RplaceServer.Types;

namespace RplaceServer;

public record ClientData
(
    string IdIpPort,
    DateTimeOffset ConnectDate,
    UidType UidType,
    string Uid
)
{
    public DateTimeOffset LastChat { get; set; } = ConnectDate;
    public DateTimeOffset Cooldown { get; set; } = ConnectDate;
    
    public bool Admin = false;
    
    public bool Vip = false;
}