using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class PlayerDisconnectedEventArgs : EventArgs
{
    public ClientMetadata Player { get; }
    
    public PlayerDisconnectedEventArgs(ClientMetadata player)
    {
        Player = player;
    }
}