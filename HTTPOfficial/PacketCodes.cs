namespace HTTPOfficial;

public enum ClientPackets
{
    DeleteAccount,
    UpdateAccount,
    CreateAccount,
    AccountCode,
    AccountInfo,
    Authenticate,
    LocateVanity
}

public enum ServerPackets
{
    Fail,
    AccountInfo,
    VanityUrl,
    
    Authorised = 130
}

public enum WorkerPackets
{
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AuthenticateVanity = 132, // TODO: Fix the mixup between the value of this and the next packet code
    AnnounceExistence = 131,
    AnnounceVanity = 133
}
