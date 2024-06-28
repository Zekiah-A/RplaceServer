namespace RplaceServer.Events;

public sealed class CanvasBackupCreatedEventArgs : EventArgs
{
    public ServerInstance Instance { get; }
    public string Name { get; }
    public DateTime Created { get;  }
    public string Path { get; }
    
    //Give them the backup name, date created, file path
    public CanvasBackupCreatedEventArgs(ServerInstance instance, string name, DateTime created, string path)
    {
        Instance = instance;
        Name = name;
        Created = created;
        Path = path;
    }
}