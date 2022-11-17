using WatsonWebsocket;

namespace RplaceServer.Events;

public class PlayerDisconnectedEventArgs : EventArgs
{
    public SocketClient Player { get; }
    
    public PlayerDisconnectedEventArgs(SocketClient player)
    {
        Player = player;
    }
}