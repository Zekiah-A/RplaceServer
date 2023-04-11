namespace WorkerOfficial;

public record ServerData(int Id, string? VanityName, int SocketPort, int WebPort)
{
    public string? VanityName = VanityName;
    public int SocketPort = SocketPort;
    public int WebPort = WebPort;
}