using RplaceServer.Types;
using WatsonWebsocket;

namespace RplaceServer.Events;

public sealed class PixelPlacementEventArgs : EventArgs
{
    public ServerInstance Instance { get; }
    public int Colour { get; }
    public uint X { get; }
    public uint Y { get; }
    public uint Index { get;  }
    public ClientMetadata Player { get; }
    public byte[] Packet { get; }
    public EventInhibitor Inhibitor { get; }

    public PixelPlacementEventArgs(ServerInstance instance, int colour, uint x, uint y, uint index, ClientMetadata player, byte[] packet, EventInhibitor inhibitor)
    {
        Instance = instance;
        Colour = colour;
        X = x;
        Y = y;
        Index = index;
        Player = player;
        Packet = packet;
        Inhibitor = inhibitor;
    }
}