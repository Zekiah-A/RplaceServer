namespace WorkerOfficial;

public class ServerData
{
    public int Id { get; init; }
    public int SocketPort;
    public int WebPort;

    public ServerData(int id, int socketPort, int webPort)
    {
        Id = id;
        SocketPort = socketPort;
        WebPort = webPort;
    }
}