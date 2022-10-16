namespace Server;

public record SocketClient
(
    string IdIpPort,
    int LastChat,
    int Voted,
    int Cooldown
);