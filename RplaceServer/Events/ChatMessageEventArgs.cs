using RplaceServer.Types;
using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class ChatMessageEventArgs : EventArgs
{
    public ClientMetadata Player { get; }
    public string Message { get; }
    public string Channel { get; }
    public string Name { get; }
    public ChatMessageType Type { get; }
    public int? X { get; }
    public int? Y { get; }
    public byte[] Packet { get; }
    public EventInhibitor Inhibitor { get; }


    //Give them the socket client instance, chat name, channel, message, message type (if canvas chat) + pos
    public ChatMessageEventArgs(ClientMetadata player, string message, string channel, string name, ChatMessageType type, byte[] packet, int? x, int? y, EventInhibitor inhibitor)
    {
        Player = player;
        Message = message;
        Channel = channel;
        Name = name;
        Type = type;
        X = x;
        Y = y;
        Packet = packet;
        Inhibitor = inhibitor;
    }
}