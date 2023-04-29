namespace WorkerOfficial;

public enum WorkerPackets : byte
{
    // Sent to clients
    Logger,
    PlayerConnected,
    PlayerDisconnected,
    BackupCreated,
    InstanceQuery,
    InstanceCreated,
    
    // Sent to auth server
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AuthenticateVanity = 131,
    AnnounceExistence = 132,
    AnnounceVanity = 133
}

public enum ServerPackets : byte
{
    Authorised = 130
}

public enum ClientPackets : byte
{
    CreateInstance,
    DeleteInstance,
    RestartInstance,
    Subscribe,
    QueryInstance,
    CreateVanity
}