namespace WorkerOfficial;

public record struct Configuration(
    int Port,
    bool UseHttps,
    string CertPath,
    string KeyPath,
    IntRange IdRange,
    IntRange SocketPortRange,
    IntRange WebPortRange,
    string AuthServerUri,
    string InstanceKey);