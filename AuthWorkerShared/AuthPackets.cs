namespace AuthWorkerShared;

public enum AuthPackets : byte
{
    CreateInstance,
    QueryInstance,
    DeleteInstance,
    Subscribe,
    Unsubscribe
}