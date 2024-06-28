using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class PlayerDisconnectedEventArgs : EventArgs
{
    public ServerInstance Instance { get; }
    public ClientMetadata Player { get; }
    
    public PlayerDisconnectedEventArgs(ServerInstance instance, ClientMetadata player)
    {
        Instance = instance;
        Player = player;
    }
}