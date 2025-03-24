using System.Text.Json.Serialization;
using AuthOfficial.Authorization;

namespace AuthOfficial.DataModel;

// Links a canvas user to an account
public class CanvasUser : AuthBase
{
    public int UserIntId { get; set; }

    public int InstanceId { get; set; }
    // Navigation property to parent instance
    [JsonIgnore]
    public Instance Instance { get; set; } = null!;

    public int? LinkedAccountId { get; set; }
    // Navigation property to linked account
    [JsonIgnore]
    public Account? LinkedAccount { get; set; } = null!;
    // Navigation property to refresh tokens
    [JsonIgnore]
    public List<CanvasUserRefreshToken> RefreshTokens { get; set; } = [];

    public CanvasUser() { }

    public CanvasUser(int userIntId, int instanceId, string securityStamp)
    {
        UserIntId = userIntId;
        InstanceId = instanceId;
        SecurityStamp = securityStamp;

        AuthType = AuthType.Account;
        CreationDate = DateTime.UtcNow;
    }
}