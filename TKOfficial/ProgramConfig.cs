namespace TKOfficial;

public record ProgramConfig
(
    bool Ssl,
    int SocketPort,
    int HttpPort,
    string CertPath,
    string KeyPath,
    string Origin,
    bool UseCloudflare,
    string CanvasFolder
);