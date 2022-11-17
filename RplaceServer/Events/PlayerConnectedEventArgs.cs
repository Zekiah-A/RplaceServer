namespace RplaceServer.Events;

public class PlayerConnectedEventArgs : EventArgs
{
    public SocketClient Player { get; }
    
    //Give them the socket client instance
    public PlayerConnectedEventArgs(SocketClient player)
    {
        Player = player;
    }
}