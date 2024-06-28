using System.Text.Json.Serialization;

namespace WorkerOfficial;

public class Configuration
{
    public int Version { get; set; }
    public int Port { get; set; }
    public bool UseHttps { get; set; }
    public string CertPath { get; set; }
    public string KeyPath { get; set; }
    public int MaxInstances { get; set; }
    public IntRange SocketPortRange { get; set; }
    public IntRange WebPortRange { get; set; }
    public string AuthServerUri { get; set; }
    public string InstanceKey { get; set; }
    public string PublicHostname { get; set; }

    [JsonIgnore]
    public const int CurrentVersion = 1;

    public Configuration(int version,
        int port,
        bool useHttps,
        string certPath,
        string keyPath,
        int maxInstances,
        IntRange socketPortRange,
        IntRange webPortRange,
        string authServerUri,
        string instanceKey,
        string publicHostname)
    {
        Version = version;
        Port = port;
        UseHttps = useHttps;
        CertPath = certPath;
        KeyPath = keyPath;
        MaxInstances = maxInstances;
        SocketPortRange = socketPortRange;
        WebPortRange = webPortRange;
        AuthServerUri = authServerUri;
        InstanceKey = instanceKey;
        PublicHostname = publicHostname;
    }
}