namespace RplaceServer.Events;

public class PixelPlacedEventArgs : EventArgs
{
    public int Colour { get; }
    public int X { get; }
    public int Y { get; }
    public int Index { get;  }
    
    public PixelPlacedEventArgs(int colour, int x, int y, int index)
    {
        Colour = colour;
        X = x;
        Y = y;
        Index = index;
    }
}