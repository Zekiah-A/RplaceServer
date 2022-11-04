using WatsonWebsocket;

namespace RplaceServer;

public class GameData
{
    //These will change frequently through the course of the game
    public byte[] Board;
    public uint Players;
    public uint Cooldown;
    public bool CaptchaEnabled;
    public List<string> Vips;
    public List<string> Bans;
    public Dictionary<ClientMetadata, SocketClient> Clients;

    //These require a server restart, or can be changed by server only
    public int BoardWidth; //Pixels
    public int BoardHeight; //Pixels
    public int PaletteSize;
    public int BackupFrequency; //Seconds
    public string WebhookUrl; //HTTP/HTTPS
    public bool UseCloudflare;
    public string CanvasFolder;
};