using RplaceServer;

namespace HTTPOfficial;

public record InstanceData(string OwnerKey, int HttPort, int WssPort, GameData GameData);