namespace RplaceServer.Events;

internal class PixelPlacedEventArgs : EventArgs
{
    public int Colour { get; }
    public int X { get; }
    public int Y { get; }
    
    public PixelPlacedEventArgs(int colour, int x, int y)
    {
        Colour = colour;
        X = x;
        Y = y;
    }
}