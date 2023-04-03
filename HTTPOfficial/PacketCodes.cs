namespace HTTPOfficial;

public enum ClientPackets
{
    DeleteAccount,
    UpdateAccount,
    CreateAccount,
    AuthenticateCreate,
    AccountInfo,
    CreateInstance,
    DeleteInstance,
    RestartInstance,
}

public enum ServerPackets
{
    Fail,
    AccountInfo
}

public enum WorkerPackets
{
    InstanceInfo
}