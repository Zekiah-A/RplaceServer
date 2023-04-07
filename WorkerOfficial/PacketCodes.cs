namespace WorkerOfficial;

public enum WorkerPackets
{
    Logger,
    AuthenticateCreate = 128,
    AuthenticateDelete = 129,
    AuthenticateManage = 130,
    AnnounceExistence = 131
}

public enum ServerPackets
{
    AuthorisedCreateInstance = 128,
    AuthorisedDeleteInstance = 129,
    Authorised = 130
}

public enum ClientPackets
{
    CreateInstance = 6,
    DeleteInstance = 7,
    RestartInstance = 8
}