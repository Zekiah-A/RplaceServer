using WatsonWebsocket;

namespace RplaceServer.Events;

public class PlayerDisconnectedEventArgs : EventArgs
{
    //Give them the socket client instance
    public PlayerDisconnectedEventArgs()
    {
        
    }
}