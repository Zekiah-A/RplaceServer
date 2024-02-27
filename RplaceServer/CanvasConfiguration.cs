namespace RplaceServer;

public interface ICanvasConfiguration
{
    public uint CooldownMs { get; set; }
    public uint BoardWidth { get; set; }
    public uint BoardHeight { get; set; }
    public List<uint>? Palette { get; set; }
}

public class ConfigureCanvasOptions : ICanvasConfiguration
{
    public uint CooldownMs { get; set; } = 1000;
    public uint BoardWidth { get; set; } = 1000;
    public uint BoardHeight { get; set; } = 1000;
    public List<uint>? Palette { get; set; } = null;
}