namespace RplaceServer.Events;

internal class ChatMessageEventArgs : EventArgs
{
    //Give them the socket client instance, chat name, channel, message, message type (if canvas chat) + pos
    public ChatMessageEventArgs()
    {
        
    }
}