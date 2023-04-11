namespace WorkerOfficial;

public enum WorkerPackets
{
    // Sent to clients
    Logger,
    PlayerConnected,
    PlayerDisconnected,
    BackupCreated,
    
    // Sent to auth server
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AnnounceExistence = 131,
    AuthenticateVanity = 132,
    AnnounceVanity = 133
}

public enum ServerPackets
{
    Authorised = 130
}

public enum ClientPackets
{
    CreateInstance = 6,
    DeleteInstance = 7,
    RestartInstance = 8,
    Subscribe = 9,
    QueryInstance = 10,
    CreateVanity = 11
}