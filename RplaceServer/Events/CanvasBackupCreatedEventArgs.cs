namespace RplaceServer.Events;

public sealed class CanvasBackupCreatedEventArgs : EventArgs
{
    public string Name { get; }
    public DateTime Created { get;  }
    public string Path { get; }
    
    //Give them the backup name, date created, file path
    public CanvasBackupCreatedEventArgs(string name, DateTime created, string path)
    {
        Name = name;
        Created = created;
        Path = path;
    }
}