namespace AuthWorkerShared;

public enum WorkerPackets : byte
{
    // Sent to auth server
    InstanceCreateStatus = 127,
    InstanceDeleteStatus = 128,
    AnnounceExistence = 129,
    
    LoggerEntry = 130,
    PlayerConnected = 131,
    PlayerDisconnected = 132,
    BackupCreated = 133,
}