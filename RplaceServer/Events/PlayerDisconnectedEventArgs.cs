using WatsonWebsocket;

namespace RplaceServer.Events;

internal class PlayerDisconnectedEventArgs : EventArgs
{
    //Give them the socket client instance
    public PlayerDisconnectedEventArgs()
    {
        
    }
}