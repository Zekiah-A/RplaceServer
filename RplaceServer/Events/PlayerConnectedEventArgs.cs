using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class PlayerConnectedEventArgs : EventArgs
{
    public ServerInstance Instance { get; }
    public ClientMetadata Player { get; }
    
    //Give them the socket client instance
    public PlayerConnectedEventArgs(ServerInstance instance, ClientMetadata player)
    {
        Instance = instance;
        Player = player;
    }
}