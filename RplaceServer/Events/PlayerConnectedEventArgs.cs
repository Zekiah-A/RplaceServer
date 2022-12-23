using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class PlayerConnectedEventArgs : EventArgs
{
    public ClientMetadata Player { get; }
    
    //Give them the socket client instance
    public PlayerConnectedEventArgs(ClientMetadata player)
    {
        Player = player;
    }
}