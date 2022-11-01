namespace RplaceServer;

//These are linked to server instance creation, can not be changed live at runtime, server must be updated.
public record ServerConfig
(
    bool Ssl,
    int SocketPort,
    int HttpPort,
    string CertPath,
    string KeyPath,
    string Origin,
    bool UseCloudflare,
    string BackupFolder
);