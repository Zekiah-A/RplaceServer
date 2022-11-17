using System.Numerics;
using RplaceServer.Enums;

namespace RplaceServer.Events;

public class ChatMessageEventArgs : EventArgs
{
    public SocketClient Client { get; }
    public string Message { get; }
    public string Channel { get; }
    public string Name { get; }
    public ChatMessageType Type { get; }
    public int? X { get; }
    public int? Y { get; }


    //Give them the socket client instance, chat name, channel, message, message type (if canvas chat) + pos
    public ChatMessageEventArgs(SocketClient client, string message, string channel, string name, ChatMessageType type, int? x, int? y)
    {
        Client = client;
        Message = message;
        Channel = channel;
        Name = name;
        Type = type;
        X = x;
        Y = y;
    }
}