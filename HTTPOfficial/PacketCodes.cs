namespace HTTPOfficial;

public enum ClientPackets
{
    DeleteAccount,
    UpdateAccount,
    CreateAccount,
    AccountCode,
    AccountInfo,
    Authenticate,
    LocateVanity,
    LocateWorkers,
    VanityAvailable
}

public enum ServerPackets
{
    // Sent to clients
    Fail,
    AccountInfo,
    VanityLocation,
    WorkerLocations,
    AvailableVanity,
    
    // Sent to worker server
    Authorised = 130
}

public enum WorkerPackets
{
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AuthenticateVanity = 131,
    AnnounceExistence = 132,
    AnnounceVanity = 133
}
