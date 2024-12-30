namespace HTTPOfficial.Configuration;

public class ServerConfiguration
{
    public string Origin { get; init; }
    public int Port { get; init; }
    public int SocketPort { get; init; }
    public bool UseHttps { get; init; }
    public string? CertPath { get; init; }
    public string? KeyPath { get; init; }
}