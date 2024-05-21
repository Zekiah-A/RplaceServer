namespace HTTPOfficial;

public enum ClientPackets : byte
{
    DeleteAccount = 0,
    CreateAccount = 2,
    AccountCode = 3,
    AccountInfo = 4,
    Authenticate = 5,
    ResolveVanity = 6,
    VanityAvailable = 8,
    RedditCreateAccount = 9,
    UpdateProfile = 11,
    ProfileInfo = 12,

    CreateInstance = 13,
    DeleteInstance = 14,
    UpdateInstance = 15,
    RestartInstance = 16,
    Subscribe = 17,
    QueryInstance = 18,
}

public enum ServerPackets : byte
{
    // Sent to clients
    AccountInfo = 1,
    VanityLocation = 2,
    WorkerLocations = 3,
    AvailableVanity = 4,
    AccountToken = 5, // UnverifiedAccount token used for normal auth
    AccountProfile = 6,
    RedditRefreshToken = 7, // Refresh token used for reddit OAuth

    // Sent to worker server
    QueryCanCreate = 130,
    SyncInstance = 131,
    HostInstance = 132,
}

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
