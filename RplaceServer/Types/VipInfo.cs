namespace RplaceServer.Types;

public record VipInfo(ClientPermissionsLevel Perms, uint CooldownMs, string? EnforcedChatName);