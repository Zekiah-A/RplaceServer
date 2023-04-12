namespace WorkerOfficial;

public enum WorkerPackets
{
    // Sent to clients
    Logger,
    PlayerConnected,
    PlayerDisconnected,
    BackupCreated,
    InstanceQuery,
    
    // Sent to auth server
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AuthenticateVanity = 131,
    AnnounceExistence = 132,
    AnnounceVanity = 133
}

public enum ServerPackets
{
    Authorised = 130
}

public enum ClientPackets
{
    CreateInstance,
    DeleteInstance,
    RestartInstance,
    Subscribe,
    QueryInstance,
    CreateVanity
}