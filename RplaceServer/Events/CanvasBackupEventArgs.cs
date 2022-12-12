namespace RplaceServer.Events;

public class CanvasBackupEventArgs : EventArgs
{
    //Give them the backup name, date created, file path
    public CanvasBackupEventArgs(string name, DateTime created, string path)
    {
        
    }
}