using RplaceServer.DataModel;
using RplaceServer.Types;

namespace RplaceServer;

/// <summary>
/// Class containing runtime socket client data and state during gameplay
/// </summary>
public class ClientData
{
    public string IpPort { get; init; }
    
    public int UserId { get; set; }
    public int SessionId { get; set; }

    public DateTimeOffset LastNameChange { get; set; }
    public DateTimeOffset LastChat { get; set; }
    public DateTimeOffset Cooldown { get; set; }
    public DateTimeOffset ConnectDate { get; init; }
    
    public Permissions Permissions;

    public ClientData(string ipPort, int userId, int sessionId, DateTimeOffset connectDate)
    {
        IpPort = ipPort;
        UserId = userId;
        SessionId = sessionId; 
        ConnectDate = connectDate;
        
        Cooldown = connectDate;
        LastChat = connectDate;
        LastNameChange = DateTimeOffset.MinValue;
    }
}