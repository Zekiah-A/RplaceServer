using System.Numerics;
using RplaceServer.Enums;

namespace RplaceServer.Events;

public sealed class ChatMessageEventArgs : EventArgs
{
    public SocketClient Player { get; }
    public string Message { get; }
    public string Channel { get; }
    public string Name { get; }
    public ChatMessageType Type { get; }
    public int? X { get; }
    public int? Y { get; }
    public byte[] Packet { get; }


    //Give them the socket client instance, chat name, channel, message, message type (if canvas chat) + pos
    public ChatMessageEventArgs(SocketClient player, string message, string channel, string name, ChatMessageType type, byte[] packet, int? x, int? y)
    {
        Player = player;
        Message = message;
        Channel = channel;
        Name = name;
        Type = type;
        X = x;
        Y = y;
        Packet = packet;
    }
}