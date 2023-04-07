using RplaceServer.Types;
using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class PixelPlacementEventArgs : EventArgs
{
    public int Colour { get; }
    public int X { get; }
    public int Y { get; }
    public int Index { get;  }
    public ClientMetadata Player { get; }
    public byte[] Packet { get; }
    public EventInhibitor Inhibitor { get; }

    public PixelPlacementEventArgs(int colour, int x, int y, int index, ClientMetadata player, byte[] packet, EventInhibitor inhibitor)
    {
        Colour = colour;
        X = x;
        Y = y;
        Index = index;
        Player = player;
        Packet = packet;
        Inhibitor = inhibitor;
    }
}