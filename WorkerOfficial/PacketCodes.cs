namespace WorkerOfficial;

public enum WorkerPackets : byte
{
    // Sent to auth server
    LoggerEntry = 130,
    PlayerConnected = 131,
    PlayerDisconnected = 132,
    BackupCreated = 133,
    SyncSuccess = 134,
    SyncFail = 135
}

public enum ServerPackets : byte
{
    // Come from auth server
    QueryCanCreate = 130,
    CreateInstance = 131,
}
