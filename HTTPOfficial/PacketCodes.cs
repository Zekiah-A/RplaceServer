namespace HTTPOfficial;

public enum ClientPackets
{
    DeleteAccount,
    UpdateAccount,
    CreateAccount,
    AccountCode,
    AccountInfo,
    Authenticate
}

public enum ServerPackets
{
    Fail,
    AccountInfo,
    
    AuthorisedCreateInstance = 128,
    AuthorisedDeleteInstance = 129,
    Authorised = 130
}

public enum WorkerPackets
{
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AnnounceExistence = 131,
    AuthenticateVanity = 132,
    AnnounceVanity = 133
}
