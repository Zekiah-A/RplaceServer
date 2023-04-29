namespace HTTPOfficial;

public enum ClientPackets : byte
{
    DeleteAccount,
    UpdateAccount,
    CreateAccount,
    AccountCode,
    AccountInfo,
    Authenticate,
    LocateVanity,
    LocateWorkers,
    VanityAvailable,
    RedditCreateAccount,
    RedditAuthenticate,
    UpdateProfile,
    ProfileInfo
}

public enum ServerPackets : byte
{
    // Sent to clients
    Fail,
    AccountInfo,
    VanityLocation,
    WorkerLocations,
    AvailableVanity,
    AccountToken,
    AccountProfile,

    // Sent to worker server
    Authorised = 130
}

public enum WorkerPackets : byte
{
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AuthenticateVanity = 131,
    AnnounceExistence = 132,
    AnnounceVanity = 133
}
